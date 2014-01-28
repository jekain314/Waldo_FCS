using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using EDSDKLib;
using Waldo_FCS;

namespace CanonSDK
{
    public struct CameraStorage
    {
        public string cardType;
        public ulong MaxCapacity;
        public ulong FreeSpaceInBytes;
        public uint numImages;
        public uint avgImageSize;
    }

    public class SDKHandler : IDisposable
    {
        #region Variables

        /// <summary>
        /// The used camera
        /// </summary>
        public Camera MainCamera { get; private set; }

        public bool PhotoInProgress { get; set; }
        public int PhotoDownloadTime { get; private set; }
        public String photoNameOnPC { get; private set; }
        public String CanonPhotoName { get; private set; }

        public bool CameraSessionOpen { get; private set; }

        StreamWriter logFile;

        String photoNameWithExtension;

        /// <summary>
        /// Directory to where photos will be saved
        /// </summary>
        public string ImageSaveDirectory { get; set; }
        /// <summary>
        /// Handles errors that happen with the SDK
        /// </summary>
        public uint Error
        {
            get { return EDSDK.EDS_ERR_OK; }
            set { if (value != EDSDK.EDS_ERR_OK) throw new Exception("SDK Error: " + value); }
        }

        Stopwatch photoTimer;
        List<Camera> CamList;


        #endregion
        

        #region Canon EDSDK Events

        public event EDSDK.EdsObjectEventHandler SDKObjectEvent;
        public event EDSDK.EdsPropertyEventHandler SDKPropertyEvent;
        public event EDSDK.EdsStateEventHandler SDKStateEvent;

        #endregion

        #region Basic SDK and Session handling

        /// <summary>
        /// Initialises the SDK and adds events
        /// </summary>
        public SDKHandler(StreamWriter _logFile, SettingsManager settings)
        {
            Error = EDSDK.EdsInitializeSDK();

            SDKStateEvent               += new EDSDK.EdsStateEventHandler(Camera_SDKStateEvent);
            SDKPropertyEvent            += new EDSDK.EdsPropertyEventHandler(Camera_SDKPropertyEvent);
            SDKObjectEvent              += new EDSDK.EdsObjectEventHandler(Camera_SDKObjectEvent);

            logFile = _logFile;

            PhotoInProgress = false;
            photoTimer = new Stopwatch();

            //get the camera list of attached cameras
            CamList = GetCameraList();

            if (CamList.Count == 0)
            {
                logFile.WriteLine("There are no attached cameras");
                MessageBox.Show("There are no attached cameras");
                return;
            }
            else if (CamList.Count > 1)
            {
                MessageBox.Show("Found more than one attached Canon camera");
                logFile.WriteLine("There is more than one attached camera");
                return;
            }

            //open the session with the first camera (only a single camera is allowed)
            OpenSession(CamList[0]);

            //label the camera type
            String cameraDescription = MainCamera.Info.szDeviceDescription;
            //label camera serial number
            String cameraSN = GetSettingString((uint)EDSDK.PropID_BodyIDEx);
            //label firmware
            String cameraFirmware = GetSettingString((uint)EDSDK.PropID_FirmwareVersion);

            logFile.WriteLine("Camera found");
            logFile.WriteLine("Camera description: " + cameraDescription);
            logFile.WriteLine("cameraSN:           " + cameraSN);
            logFile.WriteLine("cameraFirmware:     " + cameraFirmware);

            //CameraHandler.SetSetting(EDSDK.PropID_SaveTo, (uint)EDSDK.EdsSaveTo.Camera);  
            //CameraHandler.SetSetting(EDSDK.PropID_SaveTo, (uint)EDSDK.EdsSaveTo.Host);
            SetSetting(EDSDK.PropID_SaveTo, (uint)EDSDK.EdsSaveTo.Both);
            SetCapacity();  //used to tell camera there is enough room on the PC HD (see codeproject tutorial)

            //get the capacity and loading of the camera card storage drive
            //also write out the names of the images to output
            CameraStorage cameraStorage = StorageAssessment();

            double freeSpace = (double)cameraStorage.FreeSpaceInBytes * 100.0 / (double)cameraStorage.MaxCapacity;
            logFile.WriteLine("Storage media:      " + cameraStorage.cardType);
            logFile.WriteLine("Capacity (GB):      " + (cameraStorage.MaxCapacity / 1024.0).ToString("F2"));
            logFile.WriteLine("Free Space (%):     " + freeSpace.ToString("F1"));
            logFile.WriteLine("number Photos:      " + cameraStorage.numImages.ToString());
            logFile.WriteLine("Avg Photo Size:     " + (cameraStorage.avgImageSize / 1024.0).ToString("F2"));
            logFile.WriteLine();

            if (freeSpace < 50)
            {
                MessageBox.Show("Camera memory card storage  " + freeSpace.ToString("F1") + "%");
            }

            if (!Directory.Exists(@"C://_Waldo_FCS/")) Directory.CreateDirectory(@"C://_Waldo_FCS/");
            if (!Directory.Exists(@"C://_Waldo_FCS/TestImages/")) Directory.CreateDirectory(@"C://_Waldo_FCS/TestImages/");

            //default value -- update this
            ImageSaveDirectory = @"C://_Waldo_FCS/TestImages/";

            /////////////////////////////////////////////////////
            // camera settings for WaldoAir Flight Operations
            /////////////////////////////////////////////////////

            //set the aperture --- 
            SetSetting(EDSDK.PropID_Av, CameraValues.AV(settings.Camera_fStop));
            //set the shutter speed
            SetSetting(EDSDK.PropID_Tv, CameraValues.TV(settings.Camera_shutter));
            //set the ISO
            SetSetting(EDSDK.PropID_ISOSpeed, CameraValues.ISO(settings.Camera_ISO));
            //set the white balance to Daylight
            SetSetting(EDSDK.PropID_WhiteBalance, EDSDK.WhiteBalance_Daylight);
        }

        public void resetLogFile(StreamWriter _logFile) 
        {            
            logFile = _logFile;
        }

        public void setPhotoName(String _photoNameWithExtension)
        {
            photoNameWithExtension = _photoNameWithExtension;
        }

        /// <summary>
        /// Get a list of all connected cameras
        /// </summary>
        /// <returns>The camera list</returns>
        public List<Camera> GetCameraList()
        {
            IntPtr camlist;
            //Get Cameralist
            Error = EDSDK.EdsGetCameraList(out camlist);

            //Get each camera from camlist
            int c;
            Error = EDSDK.EdsGetChildCount(camlist, out c);
            List<Camera> OutCamList = new List<Camera>();
            for (int i = 0; i < c; i++)
            {
                IntPtr cptr;
                Error = EDSDK.EdsGetChildAtIndex(camlist, i, out cptr);
                OutCamList.Add(new Camera(cptr));
            }
            return OutCamList;
        }

        /// <summary>
        /// Opens a session with given camera
        /// </summary>
        /// <param name="NewCamera">The camera which will be used</param>
        public void OpenSession(Camera NewCamera)
        {
            if (CameraSessionOpen) Error = EDSDK.EdsCloseSession(MainCamera.Ref);
            if (NewCamera != null)
            {
                MainCamera = NewCamera;
                Error = EDSDK.EdsOpenSession(MainCamera.Ref);
                EDSDK.EdsSetCameraStateEventHandler(MainCamera.Ref, EDSDK.StateEvent_All, SDKStateEvent, IntPtr.Zero);
                EDSDK.EdsSetObjectEventHandler(MainCamera.Ref, EDSDK.ObjectEvent_All, SDKObjectEvent, IntPtr.Zero);
                EDSDK.EdsSetPropertyEventHandler(MainCamera.Ref, EDSDK.PropertyEvent_All, SDKPropertyEvent, IntPtr.Zero);
                CameraSessionOpen = true;
            }
        }

        /// <summary>
        /// Closes the session with the current camera
        /// </summary>
        public void CloseSession()
        {
            if (CameraSessionOpen)
            {
                Error = EDSDK.EdsCloseSession(MainCamera.Ref);
                CameraSessionOpen = false;
            }
        }

        /// <summary>
        /// Closes open session and terminates the SDK
        /// </summary>
        public void Dispose()
        {
            if (CameraSessionOpen) Error = EDSDK.EdsCloseSession(MainCamera.Ref);
            Error = EDSDK.EdsTerminateSDK();
        }

       /// <summary>
        /// assess the camera storage situation for the MainCamera
        /// </summary>
        public CameraStorage StorageAssessment()
        {
            //reference to the camera storage device (SD card or CF card)
            CameraStorage cameraStorage = new CameraStorage();

            //get the storage card volume reference for this camera
            int volumeCount = 0;
            //get the number of storage drives -- the Canon 5D Mark III has two storage devices (CF and SD)
            Error = EDSDK.EdsGetChildCount(MainCamera.Ref, out volumeCount);

            //get the capacity for each of the storage drives
            EDSDK.EdsVolumeInfo vinfoMax = new EDSDK.EdsVolumeInfo(); //contains capacity information
            ulong maxCapacity = 0;
            int driveWithMax = 0;
            IntPtr cameraVolRef;
            for (int i = 0; i < volumeCount; i++)
            {
                //get the volume reference for the ith storage drive
                Error = EDSDK.EdsGetChildAtIndex(MainCamera.Ref, i, out cameraVolRef);

                EDSDK.EdsVolumeInfo vinfo = new EDSDK.EdsVolumeInfo(); //contains capacity information
                Error = EDSDK.EdsGetVolumeInfo(cameraVolRef, out vinfo);
                Debug.WriteLine(String.Format("DriveType: {0}, Max Capacity: {1}, Free Space: {2}",
                    vinfo.szVolumeLabel, vinfo.MaxCapacity, vinfo.FreeSpaceInBytes));
                if (vinfo.MaxCapacity > maxCapacity)
                {
                    maxCapacity = vinfo.MaxCapacity;

                    vinfoMax = vinfo;
                    driveWithMax = i;
                    MainCamera.Vol = cameraVolRef;

                    //report stats on the drive with max storage 
                    //may want both drives reported -- SD card may be higher capacity by CF card may be faster
                    cameraStorage.cardType = vinfoMax.szVolumeLabel;
                    cameraStorage.MaxCapacity = vinfoMax.MaxCapacity / 1024; ;
                    cameraStorage.FreeSpaceInBytes = vinfoMax.FreeSpaceInBytes / 1024;
                }
            }

            //get the directory structure for the drive -- locate the "DCIM", "MISC" and "100Canon" folders
            int directoryCount = 0;
            IntPtr dirItem;
            EDSDK.EdsDirectoryItemInfo dirItemInfo; ;
            //get number of directories -- should be 2:  "DCIM" and "Misc"
            //locate the DCIM folder where the imagery is kept
            Error = EDSDK.EdsGetChildCount(MainCamera.Vol, out directoryCount);
            for (int i = 0; i < directoryCount; i++)
            {
                Error = EDSDK.EdsGetChildAtIndex(MainCamera.Vol, i, out dirItem);
                Error = EDSDK.EdsGetDirectoryItemInfo(dirItem, out dirItemInfo);
                if (dirItemInfo.szFileName == "DCIM")
                {
                    MainCamera.DCIMref = dirItem;
                    break;
                }
            }

            //get the number of images currently stored on the drive
            int CanonFolderCount = 0;
            // the DCIM folder has a single directory beneath it
            Error = EDSDK.EdsGetChildCount(MainCamera.DCIMref, out CanonFolderCount);
            //there should be a single folder called "100Canon" beneath the DCIM folder
            Error = EDSDK.EdsGetChildAtIndex(MainCamera.DCIMref, 0, out MainCamera.imageFolder);
            //CanonImageFolder is now the reference to the folder containing the images 

            int imageCount = 0;
            Error = EDSDK.EdsGetChildCount(MainCamera.imageFolder, out imageCount);
            Debug.WriteLine(String.Format("number of images in the DCIM folder = {0} ", imageCount));

            cameraStorage.numImages = (uint)imageCount;

            IntPtr imageFile;
            EDSDK.EdsDirectoryItemInfo imageFileInfo; ;
            //cycle through all the images getting their name on the storage drive
            cameraStorage.avgImageSize = 0;
            for (int i = 0; i < imageCount; i++)
            {
                Error = EDSDK.EdsGetChildAtIndex(MainCamera.imageFolder, i, out imageFile);
                Error = EDSDK.EdsGetDirectoryItemInfo(imageFile, out imageFileInfo);
                Debug.WriteLine(String.Format("imageName " + imageFileInfo.szFileName + " size {0} ", imageFileInfo.Size));
                cameraStorage.avgImageSize += imageFileInfo.Size;
            }
            if (cameraStorage.numImages > 0)
                cameraStorage.avgImageSize /= (cameraStorage.numImages * 1024);
            else cameraStorage.avgImageSize = 0;

            return cameraStorage;
        }        
        #endregion

        #region Eventhandling

        /// <summary>
        /// An Objectevent fired
        /// </summary>
        /// <param name="inEvent">The ObjectEvent id</param>
        /// <param name="inRef">Pointer to the object</param>
        /// <param name="inContext"></param>
        /// <returns>An EDSDK errorcode</returns>
        private uint Camera_SDKObjectEvent(uint inEvent, IntPtr inRef, IntPtr inContext)
        {
            //handle object event here
            switch (inEvent)
            {
                case EDSDK.ObjectEvent_All:
                    break;
                case EDSDK.ObjectEvent_DirItemCancelTransferDT:
                    //Debug.WriteLine(" DirItemCancelTransferDT ");
                    break;
                case EDSDK.ObjectEvent_DirItemContentChanged:
                    //Debug.WriteLine(" DirItemContentChanged ");
                    break;
                case EDSDK.ObjectEvent_DirItemCreated:
                    //Debug.WriteLine(String.Format("{0} DirItemCreated ", photoTimer.ElapsedMilliseconds) );
                    //PhotoDownloadTime = (int)photoTimer.ElapsedMilliseconds;
                    //PhotoInProgress = false;
                    break;
                case EDSDK.ObjectEvent_DirItemInfoChanged:
                    //Debug.WriteLine(" DirItemInfoChanged ");
                    break;
                case EDSDK.ObjectEvent_DirItemRemoved:
                    //Debug.WriteLine(" DirItemRemoved ");
                    break;
                case EDSDK.ObjectEvent_DirItemRequestTransfer:
                    //Debug.WriteLine(String.Format("{0} DirItemRequestTransfer ", photoTimer.ElapsedMilliseconds));
                    DownloadImage(inRef, ImageSaveDirectory);
                    PhotoDownloadTime = (int)photoTimer.ElapsedMilliseconds;
                    //Debug.WriteLine(String.Format("{0} Download Complete ", PhotoDownloadTime));
                    PhotoInProgress = false;
                    break;
                case EDSDK.ObjectEvent_DirItemRequestTransferDT:
                    //Debug.WriteLine(" DirItemRequestTransferDT ");
                    break;
                case EDSDK.ObjectEvent_FolderUpdateItems:
                    //Debug.WriteLine(" olderUpdateItems ");
                    break;
                case EDSDK.ObjectEvent_VolumeAdded:
                    //Debug.WriteLine(" VolumeAdded ");
                    break;
                case EDSDK.ObjectEvent_VolumeInfoChanged:
                    //Debug.WriteLine(String.Format("{0} VolumeInfoChanged ", photoTimer.ElapsedMilliseconds));
                    break;
                case EDSDK.ObjectEvent_VolumeRemoved:
                    //Debug.WriteLine(" VolumeRemoved ");
                    break;
                case EDSDK.ObjectEvent_VolumeUpdateItems:
                    //Debug.WriteLine(" VolumeUpdateItems ");
                    break;
            }

            return EDSDK.EDS_ERR_OK;
        }


        /// <summary>
        /// A property changed
        /// </summary>
        /// <param name="inEvent">The PropetyEvent ID</param>
        /// <param name="inPropertyID">The Property ID</param>
        /// <param name="inParameter">Event Parameter</param>
        /// <param name="inContext">...</param>
        /// <returns>An EDSDK errorcode</returns>
        private uint Camera_SDKPropertyEvent(uint inEvent, uint inPropertyID, uint inParameter, IntPtr inContext)
        {
            //Handle property event here
            switch (inEvent)
            {
                case EDSDK.PropertyEvent_All:
                    break;
                case EDSDK.PropertyEvent_PropertyChanged:
                    break;
                case EDSDK.PropertyEvent_PropertyDescChanged:
                    break;
            }

            switch (inPropertyID)
            {
                case EDSDK.PropID_AEBracket:
                    break;
                case EDSDK.PropID_AEMode:
                    break;
                case EDSDK.PropID_AEModeSelect:
                    break;
                case EDSDK.PropID_AFMode:
                    break;
                case EDSDK.PropID_Artist:
                    break;
                case EDSDK.PropID_AtCapture_Flag:
                    break;
                case EDSDK.PropID_Av:
                    break;
                case EDSDK.PropID_AvailableShots:
                    break;
                case EDSDK.PropID_BatteryLevel:
                    break;
                case EDSDK.PropID_BatteryQuality:
                    break;
                case EDSDK.PropID_BodyIDEx:
                    break;
                case EDSDK.PropID_Bracket:
                    break;
                case EDSDK.PropID_CFn:
                    break;
                case EDSDK.PropID_ClickWBPoint:
                    break;
                case EDSDK.PropID_ColorMatrix:
                    break;
                case EDSDK.PropID_ColorSaturation:
                    break;
                case EDSDK.PropID_ColorSpace:
                    break;
                case EDSDK.PropID_ColorTemperature:
                    break;
                case EDSDK.PropID_ColorTone:
                    break;
                case EDSDK.PropID_Contrast:
                    break;
                case EDSDK.PropID_Copyright:
                    break;
                case EDSDK.PropID_DateTime:
                    break;
                case EDSDK.PropID_DepthOfField:
                    break;
                case EDSDK.PropID_DigitalExposure:
                    break;
                case EDSDK.PropID_DriveMode:
                    break;
                case EDSDK.PropID_EFCompensation:
                    break;
                case EDSDK.PropID_Evf_AFMode:
                    break;
                case EDSDK.PropID_Evf_ColorTemperature:
                    break;
                case EDSDK.PropID_Evf_DepthOfFieldPreview:
                    break;
                case EDSDK.PropID_Evf_FocusAid:
                    break;
                case EDSDK.PropID_Evf_Histogram:
                    break;
                case EDSDK.PropID_Evf_HistogramStatus:
                    break;
                case EDSDK.PropID_Evf_ImagePosition:
                    break;
                case EDSDK.PropID_Evf_Mode:
                    break;
                case EDSDK.PropID_Evf_OutputDevice:
                    break;
                case EDSDK.PropID_Evf_WhiteBalance:
                    break;
                case EDSDK.PropID_Evf_Zoom:
                    break;
                case EDSDK.PropID_Evf_ZoomPosition:
                    break;
                case EDSDK.PropID_ExposureCompensation:
                    break;
                case EDSDK.PropID_FEBracket:
                    break;
                case EDSDK.PropID_FilterEffect:
                    break;
                case EDSDK.PropID_FirmwareVersion:
                    break;
                case EDSDK.PropID_FlashCompensation:
                    break;
                case EDSDK.PropID_FlashMode:
                    break;
                case EDSDK.PropID_FlashOn:
                    break;
                case EDSDK.PropID_FocalLength:
                    break;
                case EDSDK.PropID_FocusInfo:
                    break;
                case EDSDK.PropID_GPSAltitude:
                    break;
                case EDSDK.PropID_GPSAltitudeRef:
                    break;
                case EDSDK.PropID_GPSDateStamp:
                    break;
                case EDSDK.PropID_GPSLatitude:
                    break;
                case EDSDK.PropID_GPSLatitudeRef:
                    break;
                case EDSDK.PropID_GPSLongitude:
                    break;
                case EDSDK.PropID_GPSLongitudeRef:
                    break;
                case EDSDK.PropID_GPSMapDatum:
                    break;
                case EDSDK.PropID_GPSSatellites:
                    break;
                case EDSDK.PropID_GPSStatus:
                    break;
                case EDSDK.PropID_GPSTimeStamp:
                    break;
                case EDSDK.PropID_GPSVersionID:
                    break;
                case EDSDK.PropID_HDDirectoryStructure:
                    break;
                case EDSDK.PropID_ICCProfile:
                    break;
                case EDSDK.PropID_ImageQuality:
                    break;
                case EDSDK.PropID_ISOBracket:
                    break;
                case EDSDK.PropID_ISOSpeed:
                    break;
                case EDSDK.PropID_JpegQuality:
                    break;
                case EDSDK.PropID_LensName:
                    break;
                case EDSDK.PropID_LensStatus:
                    break;
                case EDSDK.PropID_Linear:
                    break;
                case EDSDK.PropID_MakerName:
                    break;
                case EDSDK.PropID_MeteringMode:
                    break;
                case EDSDK.PropID_NoiseReduction:
                    break;
                case EDSDK.PropID_Orientation:
                    break;
                case EDSDK.PropID_OwnerName:
                    break;
                case EDSDK.PropID_ParameterSet:
                    break;
                case EDSDK.PropID_PhotoEffect:
                    break;
                case EDSDK.PropID_PictureStyle:
                    break;
                case EDSDK.PropID_PictureStyleCaption:
                    break;
                case EDSDK.PropID_PictureStyleDesc:
                    break;
                case EDSDK.PropID_ProductName:
                    break;
                case EDSDK.PropID_Record:
                    break;
                case EDSDK.PropID_RedEye:
                    break;
                case EDSDK.PropID_SaveTo:
                    break;
                case EDSDK.PropID_Sharpness:
                    break;
                case EDSDK.PropID_ToneCurve:
                    break;
                case EDSDK.PropID_ToningEffect:
                    break;
                case EDSDK.PropID_Tv:
                    break;
                case EDSDK.PropID_Unknown:
                    break;
                case EDSDK.PropID_WBCoeffs:
                    break;
                case EDSDK.PropID_WhiteBalance:
                    break;
                case EDSDK.PropID_WhiteBalanceBracket:
                    break;
                case EDSDK.PropID_WhiteBalanceShift:
                    break;
            }
            return EDSDK.EDS_ERR_OK;
        }

        /// <summary>
        /// The camera state changed
        /// </summary>
        /// <param name="inEvent">The StateEvent ID</param>
        /// <param name="inParameter">Parameter from this event</param>
        /// <param name="inContext">...</param>
        /// <returns>An EDSDK errorcode</returns>
        private uint Camera_SDKStateEvent(uint inEvent, uint inParameter, IntPtr inContext)
        {
            //Handle state event here
            switch (inEvent)
            {
                case EDSDK.StateEvent_All:
                    break;
                case EDSDK.StateEvent_AfResult:
                    break;
                case EDSDK.StateEvent_BulbExposureTime:
                    break;
                case EDSDK.StateEvent_CaptureError:
                    break;
                case EDSDK.StateEvent_InternalError:
                    break;
                case EDSDK.StateEvent_JobStatusChanged:
                    break;
                case EDSDK.StateEvent_Shutdown:
                    break;
                case EDSDK.StateEvent_ShutDownTimerUpdate:
                    break;
                case EDSDK.StateEvent_WillSoonShutDown:
                    break;
            }
            return EDSDK.EDS_ERR_OK;
        }

        #endregion

        #region Camera commands

        /// <summary>
        /// Downloads an image to given directory
        /// </summary>
        /// <param name="Info">Pointer to the object. Get it from the SDKObjectEvent.</param>
        /// <param name="directory"></param>
        public void DownloadImage(IntPtr ObjectPointer, string directory)
        {
            //The directory item in this case will be a photo file
            EDSDK.EdsDirectoryItemInfo dirInfo;
            IntPtr streamRef;
            Error = EDSDK.EdsGetDirectoryItemInfo(ObjectPointer, out dirInfo);  //get the photo information

            //file name on the Canon storage media
            CanonPhotoName = dirInfo.szFileName;

            //photo file name after transfer to PC  -- photoNameWithExtension must be set prio tor the trigger command
            string CurrentPhoto = Path.Combine(directory, photoNameWithExtension);

            //create the filestream that will transfer the Canon photo to the PC
            Error = EDSDK.EdsCreateFileStream(CurrentPhoto, EDSDK.EdsFileCreateDisposition.CreateAlways, EDSDK.EdsAccess.ReadWrite, out streamRef);      
            
            //transfer the photo file in blocks undil done
            uint blockSize = 1024 * 1024;
            uint remainingBytes = dirInfo.Size;
            do
            {
                if (remainingBytes < blockSize) { blockSize = (uint)(remainingBytes / 512) * 512; }
                remainingBytes -= blockSize;
                Error = EDSDK.EdsDownload(ObjectPointer, blockSize, streamRef);
            } while (remainingBytes > 512);

            Error = EDSDK.EdsDownload(ObjectPointer, remainingBytes, streamRef);
            Error = EDSDK.EdsDownloadComplete(ObjectPointer);

            Error = EDSDK.EdsRelease(ObjectPointer);
            Error = EDSDK.EdsRelease(streamRef);
        }
        
        /// <summary>
        /// Gets the list of possible values for the current camera to set.
        /// Only the PropertyIDs "AEModeSelect", "ISO", "Av", "Tv", "MeteringMode" and "ExposureCompensation" are allowed.
        /// </summary>
        /// <param name="PropID">The property ID</param>
        /// <returns>A list of available values for the given property ID</returns>
        public List<int> GetSettingsList(uint PropID)
        {
            if (MainCamera.Ref != IntPtr.Zero)
            {
                if (PropID == EDSDK.PropID_AEModeSelect || PropID == EDSDK.PropID_ISOSpeed || PropID == EDSDK.PropID_Av
                    || PropID == EDSDK.PropID_Tv || PropID == EDSDK.PropID_MeteringMode || PropID == EDSDK.PropID_ExposureCompensation)
                {
                    EDSDK.EdsPropertyDesc des;
                    Error = EDSDK.EdsGetPropertyDesc(MainCamera.Ref, PropID, out des);
                    return des.PropDesc.Take(des.NumElements).ToList();
                }
                else throw new ArgumentException("Method cannot be used with this Property ID");
            }
            else { throw new ArgumentNullException("Camera or camera reference is null/zero"); }
        }

        /// <summary>
        /// Gets the current setting of given property ID for int properties
        /// </summary>
        /// <param name="PropID">The property ID</param>
        /// <returns>The current setting of the camera</returns>
        public uint GetSetting(uint PropID)
        {
            if (MainCamera.Ref != IntPtr.Zero)
            {
                unsafe
                {
                    uint property = 0;
                    EDSDK.EdsDataType dataType;
                    int dataSize;
                    IntPtr ptr = new IntPtr(&property);
                    Error = EDSDK.EdsGetPropertySize(MainCamera.Ref, PropID, 0, out dataType, out dataSize);
                    Error = EDSDK.EdsGetPropertyData(MainCamera.Ref, PropID, 0, dataSize, ptr);
                    return property;
                }
            }
            else { throw new ArgumentNullException("Camera or camera reference is null/zero"); }
        }

        /// <summary>
        /// Gets the current setting of given property ID for Text properties
        /// </summary>
        /// <param name="PropID">The property ID</param>
        /// <returns>The current setting of the camera</returns>        
        public String GetSettingString(uint PropID)
        {
            if (MainCamera.Ref != IntPtr.Zero)
            {
                unsafe
                {
                    EDSDK.EdsDataType dataType;
                    int dataSize;
                    Error = EDSDK.EdsGetPropertySize(MainCamera.Ref, PropID, 0, out dataType, out dataSize);
                    String str = new String('*', dataSize);
                    IntPtr intPtr_str = System.Runtime.InteropServices.Marshal.StringToHGlobalAnsi(str);
                    Error = EDSDK.EdsGetPropertyData(MainCamera.Ref, PropID, 0, dataSize, intPtr_str);
                    return System.Runtime.InteropServices.Marshal.PtrToStringAnsi(intPtr_str); ;
                }
            }
            else { throw new ArgumentNullException("Camera or camera reference is null/zero"); }
        }

        /// <summary>
        /// Sets a value for the given property ID
        /// </summary>
        /// <param name="PropID">The property ID</param>
        /// <param name="Value">The value which will be set</param>
        public void SetSetting(uint PropID, uint Value)
        {
            if (MainCamera.Ref != IntPtr.Zero)
            {
                int propsize;
                EDSDK.EdsDataType proptype;
                Error = EDSDK.EdsGetPropertySize(MainCamera.Ref, PropID, 0, out proptype, out propsize);
                Error = EDSDK.EdsSetPropertyData(MainCamera.Ref, PropID, 0, propsize, Value);
            }
            else { throw new ArgumentNullException("Camera or camera reference is null/zero"); }
        }

        /// <summary>
        /// Tells the camera that there is enough space on the HDD if SaveTo is set to Host
        /// This method does not use the actual free space!
        /// </summary>
        public void SetCapacity()
        {
            var capacity = new EDSDK.EdsCapacity();

            capacity.Reset = 1;
            capacity.BytesPerSector = 0x1000;
            capacity.NumberOfFreeClusters = 0x7FFFFFFF;

            Error = EDSDK.EdsSetCapacity(MainCamera.Ref, capacity);
        }

        /// <summary>
        /// Locks or unlocks the cameras UI
        /// </summary>
        /// <param name="LockState">True for locked, false to unlock</param>
        public void UILock(bool LockState)
        {
            if (LockState == true) Error = EDSDK.EdsSendStatusCommand(MainCamera.Ref, EDSDK.CameraState_UILock, 0);
            else Error = EDSDK.EdsSendStatusCommand(MainCamera.Ref, EDSDK.CameraState_UIUnLock, 0);
        }

        /// <summary>
        /// Takes a photo with the current camera settings
        /// </summary>
        public void TakePhoto()
        {
            ///////////////////////////////////////////////////////////////////////////////////////////////////////////
            //this procedure sends a command to take a picture with the Canon camera
            //it also sets a photo name that will be used in the delegate that will respond to the picture success
            //In the delegate, the picture is downloaded to the camera memory and to the PC harddrive
            //The Canon has its own unique name and the PC name will be as provided here
            ///////////////////////////////////////////////////////////////////////////////////////////////////////////

            //set a flag to indicate the photo is in progress on the camera
            //this is set to false in the delegate (event)w when all is done
            PhotoInProgress = true;
            photoTimer.Restart();
            Debug.WriteLine("             Photo Triggered .... ");

            //this thread just commands a trigger and waits to ensure that the trigger was accepted by the camera
            new Thread(delegate()
            {
                int BusyCount = 0;
                uint err = EDSDK.EDS_ERR_OK;
                while (BusyCount < 20)
                {
                    err = EDSDK.EdsSendCommand(MainCamera.Ref, EDSDK.CameraCommand_TakePicture, 0);
                    if (err == EDSDK.EDS_ERR_DEVICE_BUSY) { BusyCount++; Thread.Sleep(50); }
                    else { break; }
                }
                Error = err;
                //what to do if the BusyCount exceeds 20 ??? 
            }).Start();

            //maybe establish a "photo" stats structure and make an accessor?
            //   Canon name
            //   time to get to canon storage
            //   time to get to PC HD
            //   PC name
            //   image size ??
        }

        #endregion
    }

    public class Camera
    {
        internal IntPtr Ref;
        internal IntPtr Vol;            //added to hold the camera storage drive (volume) -- 5D3 has two drives!!
        internal IntPtr imageFolder;    //added to hold pointer to the image folder (called "100Canon")
        internal IntPtr DCIMref;        //added to hold the DCIM folder reference

        public EDSDK.EdsDeviceInfo Info { get; private set; }

        public uint Error
        {
            get { return EDSDK.EDS_ERR_OK; }
            set { if (value != EDSDK.EDS_ERR_OK) throw new Exception("SDK Error: " + value); }
        }

        public Camera(IntPtr Reference)
        {
            if (Reference == IntPtr.Zero) throw new ArgumentNullException("Camera pointer is zero");
            this.Ref = Reference;
            EDSDK.EdsDeviceInfo dinfo;
            Error = EDSDK.EdsGetDeviceInfo(Reference, out dinfo);
            this.Info = dinfo;
        }
    }

    public static class CameraValues
    {
        private static CultureInfo cInfo = new CultureInfo("en-US");

        public static string AV(uint v)
        {
            switch (v)
            {
                case 0x08:
                    return "1";
                case 0x40:
                    return "11";
                case 0x0B:
                    return "1.1";
                case 0x43:
                    return "13 (1/3)";
                case 0x0C:
                    return "1.2";
                case 0x44:
                    return "13";
                case 0x0D:
                    return "1.2 (1/3)";
                case 0x45:
                    return "14";
                case 0x10:
                    return "1.4";
                case 0x48:
                    return "16";
                case 0x13:
                    return "1.6";
                case 0x4B:
                    return "18";
                case 0x14:
                    return "1.8";
                case 0x4C:
                    return "19";
                case 0x15:
                    return "1.8 (1/3)";
                case 0x4D:
                    return "20";
                case 0x18:
                    return "2";
                case 0x50:
                    return "22";
                case 0x1B:
                    return "2.2";
                case 0x53:
                    return "25";
                case 0x1C:
                    return "2.5";
                case 0x54:
                    return "27";
                case 0x1D:
                    return "2.5 (1/3)";
                case 0x55:
                    return "29";
                case 0x20:
                    return "2.8";
                case 0x58:
                    return "32";
                case 0x23:
                    return "3.2";
                case 0x5B:
                    return "36";
                case 0x24:
                    return "3.5";
                case 0x5C:
                    return "38";
                case 0x25:
                    return "3.5 (1/3)";
                case 0x5D:
                    return "40";
                case 0x28:
                    return "4";
                case 0x60:
                    return "45";
                case 0x2B:
                    return "4.5";
                case 0x63:
                    return "51";
                case 0x2C:
                    return "4.5 (1/3)";
                case 0x64:
                    return "54";
                case 0x2D:
                    return "5.0";
                case 0x65:
                    return "57";
                case 0x30:
                    return "5.6";
                case 0x68:
                    return "64";
                case 0x33:
                    return "6.3";
                case 0x6B:
                    return "72";
                case 0x34:
                    return "6.7";
                case 0x6C:
                    return "76";
                case 0x35:
                    return "7.1";
                case 0x6D:
                    return "80";
                case 0x38:
                    return " 8";
                case 0x70:
                    return "91";
                case 0x3B:
                    return "9";
                case 0x3C:
                    return "9.5";
                case 0x3D:
                    return "10";

                case 0xffffffff:
                default:
                    return "N/A";
            }
        }

        public static string ISO(uint v)
        {
            switch (v)
            {
                case 0x00000000:
                    return "Auto ISO";
                case 0x00000028:
                    return "ISO 6";
                case 0x00000030:
                    return "ISO 12";
                case 0x00000038:
                    return "ISO 25";
                case 0x00000040:
                    return "ISO 50";
                case 0x00000048:
                    return "ISO 100";
                case 0x0000004b:
                    return "ISO 125";
                case 0x0000004d:
                    return "ISO 160";
                case 0x00000050:
                    return "ISO 200";
                case 0x00000053:
                    return "ISO 250";
                case 0x00000055:
                    return "ISO 320";
                case 0x00000058:
                    return "ISO 400";
                case 0x0000005b:
                    return "ISO 500";
                case 0x0000005d:
                    return "ISO 640";
                case 0x00000060:
                    return "ISO 800";
                case 0x00000063:
                    return "ISO 1000";
                case 0x00000065:
                    return "ISO 1250";
                case 0x00000068:
                    return "ISO 1600";
                case 0x00000070:
                    return "ISO 3200";
                case 0x00000078:
                    return "ISO 6400";
                case 0x00000080:
                    return "ISO 12800";
                case 0x00000088:
                    return "ISO 25600";
                case 0x00000090:
                    return "ISO 51200";
                case 0x00000098:
                    return "ISO 102400";
                case 0xffffffff:
                default:
                    return "N/A";
            }
        }

        public static string TV(uint v)
        {
            switch (v)
            {
                case 0x0C:
                    return "Bulb";
                case 0x5D:
                    return "1/25";
                case 0x10:
                    return "30\"";
                case 0x60:
                    return "1/30";
                case 0x13:
                    return "25\"";
                case 0x63:
                    return "1/40";
                case 0x14:
                    return "20\"";
                case 0x64:
                    return "1/45";
                case 0x15:
                    return "20\" (1/3)";
                case 0x65:
                    return "1/50";
                case 0x18:
                    return "15\"";
                case 0x68:
                    return "1/60";
                case 0x1B:
                    return "13\"";
                case 0x6B:
                    return "1/80";
                case 0x1C:
                    return "10\"";
                case 0x6C:
                    return "1/90";
                case 0x1D:
                    return "10\" (1/3)";
                case 0x6D:
                    return "1/100";
                case 0x20:
                    return "8\"";
                case 0x70:
                    return "1/125";
                case 0x23:
                    return "6\" (1/3)";
                case 0x73:
                    return "1/160";
                case 0x24:
                    return "6\"";
                case 0x74:
                    return "1/180";
                case 0x25:
                    return "5\"";
                case 0x75:
                    return "1/200";
                case 0x28:
                    return "4\"";
                case 0x78:
                    return "1/250";
                case 0x2B:
                    return "3\"2";
                case 0x7B:
                    return "1/320";
                case 0x2C:
                    return "3\"";
                case 0x7C:
                    return "1/350";
                case 0x2D:
                    return "2\"5";
                case 0x7D:
                    return "1/400";
                case 0x30:
                    return "2\"";
                case 0x80:
                    return "1/500";
                case 0x33:
                    return "1\"6";
                case 0x83:
                    return "1/640";
                case 0x34:
                    return "1\"5";
                case 0x84:
                    return "1/750";
                case 0x35:
                    return "1\"3";
                case 0x85:
                    return "1/800";
                case 0x38:
                    return "1\"";
                case 0x88:
                    return "1/1000";
                case 0x3B:
                    return "0\"8";
                case 0x8B:
                    return "1/1250";
                case 0x3C:
                    return "0\"7";
                case 0x8C:
                    return "1/1500";
                case 0x3D:
                    return "0\"6";
                case 0x8D:
                    return "1/1600";
                case 0x40:
                    return "0\"5";
                case 0x90:
                    return "1/2000";
                case 0x43:
                    return "0\"4";
                case 0x93:
                    return "1/2500";
                case 0x44:
                    return "0\"3";
                case 0x94:
                    return "1/3000";
                case 0x45:
                    return "0\"3 (1/3)";
                case 0x95:
                    return "1/3200";
                case 0x48:
                    return "1/4";
                case 0x98:
                    return "1/4000";
                case 0x4B:
                    return "1/5";
                case 0x9B:
                    return "1/5000";
                case 0x4C:
                    return "1/6";
                case 0x9C:
                    return "1/6000";
                case 0x4D:
                    return "1/6 (1/3)";
                case 0x9D:
                    return "1/6400";
                case 0x50:
                    return "1/8";
                case 0xA0:
                    return "1/8000";
                case 0x53:
                    return "1/10 (1/3)";
                case 0x54:
                    return "1/10";
                case 0x55:
                    return "1/13";
                case 0x58:
                    return "1/15";
                case 0x5B:
                    return "1/20 (1/3)";
                case 0x5C:
                    return "1/20";

                case 0xffffffff:
                default:
                    return "N/A";
            }
        }


        public static uint AV(string v)
        {
            switch (v)
            {
                case "1":
                    return 0x08;
                case "11":
                    return 0x40;
                case "1.1":
                    return 0x0B;
                case "13 (1/3)":
                    return 0x43;
                case "1.2":
                    return 0x0C;
                case "13":
                    return 0x44;
                case "1.2 (1/3)":
                    return 0x0D;
                case "14":
                    return 0x45;
                case "1.4":
                    return 0x10;
                case "16":
                    return 0x48;
                case "1.6":
                    return 0x13;
                case "18":
                    return 0x4B;
                case "1.8":
                    return 0x14;
                case "19":
                    return 0x4C;
                case "1.8 (1/3)":
                    return 0x15;
                case "20":
                    return 0x4D;
                case "2":
                    return 0x18;
                case "22":
                    return 0x50;
                case "2.2":
                    return 0x1B;
                case "25":
                    return 0x53;
                case "2.5":
                    return 0x1C;
                case "27":
                    return 0x54;
                case "2.5 (1/3)":
                    return 0x1D;
                case "29":
                    return 0x55;
                case "2.8":
                    return 0x20;
                case "32":
                    return 0x58;
                case "3.2":
                    return 0x23;
                case "36":
                    return 0x5B;
                case "3.5":
                    return 0x24;
                case "38":
                    return 0x5C;
                case "3.5 (1/3)":
                    return 0x25;
                case "40":
                    return 0x5D;
                case "4":
                    return 0x28;
                case "45":
                    return 0x60;
                case "4.5":
                    return 0x2B;
                case "51":
                    return 0x63;
                case "4.5 (1/3)":
                    return 0x2C;
                case "54":
                    return 0x64;
                case "5.0":
                    return 0x2D;
                case "57":
                    return 0x65;
                case "5.6":
                    return 0x30;
                case "64":
                    return 0x68;
                case "6.3":
                    return 0x33;
                case "72":
                    return 0x6B;
                case "6.7":
                    return 0x34;
                case "76":
                    return 0x6C;
                case "7.1":
                    return 0x35;
                case "80":
                    return 0x6D;
                case " 8":
                    return 0x38;
                case "91":
                    return 0x70;
                case "9":
                    return 0x3B;
                case "9.5":
                    return 0x3C;
                case "10":
                    return 0x3D;

                case "N/A":
                default:
                    return 0xffffffff;
            }
        }

        public static uint ISO(string v)
        {
            switch (v)
            {
                case "Auto ISO":
                    return 0x00000000;
                case "ISO 6":
                    return 0x00000028;
                case "ISO 12":
                    return 0x00000030;
                case "ISO 25":
                    return 0x00000038;
                case "ISO 50":
                    return 0x00000040;
                case "ISO 100":
                    return 0x00000048;
                case "ISO 125":
                    return 0x0000004b;
                case "ISO 160":
                    return 0x0000004d;
                case "ISO 200":
                    return 0x00000050;
                case "ISO 250":
                    return 0x00000053;
                case "ISO 320":
                    return 0x00000055;
                case "ISO 400":
                    return 0x00000058;
                case "ISO 500":
                    return 0x0000005b;
                case "ISO 640":
                    return 0x0000005d;
                case "ISO 800":
                    return 0x00000060;
                case "ISO 1000":
                    return 0x00000063;
                case "ISO 1250":
                    return 0x00000065;
                case "ISO 1600":
                    return 0x00000068;
                case "ISO 3200":
                    return 0x00000070;
                case "ISO 6400":
                    return 0x00000078;
                case "ISO 12800":
                    return 0x00000080;
                case "ISO 25600":
                    return 0x00000088;
                case "ISO 51200":
                    return 0x00000090;
                case "ISO 102400":
                    return 0x00000098;

                case "N/A":
                default:
                    return 0xffffffff;
            }
        }

        public static uint TV(string v)
        {
            switch (v)
            {
                case "Bulb":
                    return 0x0C;
                case "1/25":
                    return 0x5D;
                case "30\"":
                    return 0x10;
                case "1/30":
                    return 0x60;
                case "25\"":
                    return 0x13;
                case "1/40":
                    return 0x63;
                case "20\"":
                    return 0x14;
                case "1/45":
                    return 0x64;
                case "20\" (1/3)":
                    return 0x15;
                case "1/50":
                    return 0x65;
                case "15\"":
                    return 0x18;
                case "1/60":
                    return 0x68;
                case "13\"":
                    return 0x1B;
                case "1/80":
                    return 0x6B;
                case "10\"":
                    return 0x1C;
                case "1/90":
                    return 0x6C;
                case "10\" (1/3)":
                    return 0x1D;
                case "1/100":
                    return 0x6D;
                case "8\"":
                    return 0x20;
                case "1/125":
                    return 0x70;
                case "6\" (1/3)":
                    return 0x23;
                case "1/160":
                    return 0x73;
                case "6\"":
                    return 0x24;
                case "1/180":
                    return 0x74;
                case "5\"":
                    return 0x25;
                case "1/200":
                    return 0x75;
                case "4\"":
                    return 0x28;
                case "1/250":
                    return 0x78;
                case "3\"2":
                    return 0x2B;
                case "1/320":
                    return 0x7B;
                case "3\"":
                    return 0x2C;
                case "1/350":
                    return 0x7C;
                case "2\"5":
                    return 0x2D;
                case "1/400":
                    return 0x7D;
                case "2\"":
                    return 0x30;
                case "1/500":
                    return 0x80;
                case "1\"6":
                    return 0x33;
                case "1/640":
                    return 0x83;
                case "1\"5":
                    return 0x34;
                case "1/750":
                    return 0x84;
                case "1\"3":
                    return 0x35;
                case "1/800":
                    return 0x85;
                case "1\"":
                    return 0x38;
                case "1/1000":
                    return 0x88;
                case "0\"8":
                    return 0x3B;
                case "1/1250":
                    return 0x8B;
                case "0\"7":
                    return 0x3C;
                case "1/1500":
                    return 0x8C;
                case "0\"6":
                    return 0x3D;
                case "1/1600":
                    return 0x8D;
                case "0\"5":
                    return 0x40;
                case "1/2000":
                    return 0x90;
                case "0\"4":
                    return 0x43;
                case "1/2500":
                    return 0x93;
                case "0\"3":
                    return 0x44;
                case "1/3000":
                    return 0x94;
                case "0\"3 (1/3)":
                    return 0x45;
                case "1/3200":
                    return 0x95;
                case "1/4":
                    return 0x48;
                case "1/4000":
                    return 0x98;
                case "1/5":
                    return 0x4B;
                case "1/5000":
                    return 0x9B;
                case "1/6":
                    return 0x4C;
                case "1/6000":
                    return 0x9C;
                case "1/6 (1/3)":
                    return 0x4D;
                case "1/6400":
                    return 0x9D;
                case "1/8":
                    return 0x50;
                case "1/8000":
                    return 0xA0;
                case "1/10 (1/3)":
                    return 0x53;
                case "1/10":
                    return 0x54;
                case "1/13":
                    return 0x55;
                case "1/15":
                    return 0x58;
                case "1/20 (1/3)":
                    return 0x5B;
                case "1/20":
                    return 0x5C;

                case "N/A":
                default:
                    return 0xffffffff;
            }
        }
    }
}
