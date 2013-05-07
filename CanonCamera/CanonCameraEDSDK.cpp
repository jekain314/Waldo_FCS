// This is the main DLL file.

#include "stdafx.h"

#include <stdio.h>
#include <string.h>
#include "EDSDK.h"
#include "CanonCameraEDSDK.h"

using namespace System;
using namespace System::IO;
using namespace System::Drawing;
using namespace System::Runtime::InteropServices;

namespace CanonCameraEDSDK {

		CanonCamera::CanonCamera(void)
	{
		twLog = File::CreateText("C:\\temp\\cam.txt");
		eosErr_ = EDS_ERR_OK;
		triggerFired_ = false;
		imageReady_ = false;
		liveFeed_ = false;
		// Initialize SDK
		eosErr_ = EdsInitializeSDK();
		if (eosErr_ != EDS_ERR_OK)
		{
			System::Exception ^exc;
			exc = gcnew System::Exception("Could not load Canon camera library.");
			throw exc;
		}
		//eosCamera_ = NULL;
		try
		{
			GetCameras();
		}
		catch(...)
		{
			throw;
		}

		if (eosErr_ != EDS_ERR_OK)
		{
			System::Exception ^exc;
			exc = gcnew System::Exception("Could not find camera.");
			throw exc;
		}

		// Set event handler
		objDelegate = gcnew handleObjectDelegate(this, &CanonCamera::handleObjectEvent);
		IntPtr objPtr = Marshal::GetFunctionPointerForDelegate(objDelegate);
		propDelegate = gcnew handlePropertyDelegate(this, &CanonCamera::handlePropertyEvent);
		IntPtr propPtr = Marshal::GetFunctionPointerForDelegate(propDelegate);
		stateDelegate = gcnew handleStateDelegate(this, &CanonCamera::handleStateEvent);
		IntPtr statePtr = Marshal::GetFunctionPointerForDelegate(stateDelegate);

		if (eosCameras_ != NULL)
		{
			EdsUInt32 count;
			eosErr_ = EdsGetChildCount(eosCameras_, &count);
			if ((count > 0) && (eosErr_ == EDS_ERR_OK))
			{
				for (unsigned int c = 0; c < count; c++)
				{
					EdsCameraRef camera;
					eosErr_ = EdsGetChildAtIndex(eosCameras_, c, &camera);
					if (camera != NULL)
					{
						// Set event handler
						eosErr_ = EdsSetObjectEventHandler(camera, kEdsObjectEvent_All,
															(EdsObjectEventHandler)objPtr.ToPointer(), NULL);
															//&handleObjectEvent, NULL);
						if (eosErr_ != EDS_ERR_OK)
						{
							System::Exception ^exc;
							exc = gcnew System::Exception("Could not establish object event handler.");
							throw exc;
						}
	
						// Set event handler
						eosErr_ = EdsSetPropertyEventHandler(camera, kEdsPropertyEvent_All,
															(EdsPropertyEventHandler)propPtr.ToPointer(), NULL);
						if (eosErr_ != EDS_ERR_OK)
						{
							System::Exception ^exc;
							exc = gcnew System::Exception("Could not establish property event handler.");
							throw exc;
						}
	
						// Set event handler
						eosErr_ = EdsSetPropertyEventHandler(camera, kEdsStateEvent_All,
															(EdsPropertyEventHandler)statePtr.ToPointer(), NULL);
						if (eosErr_ != EDS_ERR_OK)
						{
							System::Exception ^exc;
							exc = gcnew System::Exception("Could not establish state event handler.");
							throw exc;
						}

						// Open session with camera
						eosErr_ = EdsOpenSession(camera);
						if (eosErr_ != EDS_ERR_OK)
						{
							System::Exception ^exc;
							exc = gcnew System::Exception("Could not establish camera session.");
							throw exc;
						}
						//EdsChar prefBuf[EDS_MAX_NAME+1];
						EdsUInt32 savePref;
						eosErr_ = EdsGetPropertyData(camera, kEdsPropID_ImageQuality, 0, sizeof(savePref), (void *)&savePref);
						//savePref = 0x00640014;
						savePref = 0x00640F0F;
						savePref = 0x00130F0F;
						eosErr_ = EdsSetPropertyData(camera, kEdsPropID_ImageQuality, 0, sizeof(savePref), (void *)&savePref);
						eosErr_ = EdsGetPropertyData(camera, kEdsPropID_ImageQuality, 0, sizeof(savePref), (void *)&savePref);
						//kEdsSaveTo_Camera       =   1,
						//kEdsSaveTo_Host         =   2,
						savePref = 2; // to host computer
						savePref = kEdsSaveTo_Camera; // to sd card
						eosErr_ = EdsSetPropertyData(camera, kEdsPropID_SaveTo, 0, sizeof(savePref), (void *)&savePref);
						imgPath_ = ".\\";
					}
				}
			}

			SetShutters(0.5);
		}
	}

	CanonCamera::~CanonCamera(void)
	{
		EdsUInt32 count;
		if (eosCameras_ != NULL)
		{
			eosErr_ = EdsGetChildCount(eosCameras_, &count);
			if ((count > 0) && (eosErr_ == EDS_ERR_OK))
			{
				for (unsigned int c = 0; c < count; c++)
				{
					EdsCameraRef camera;
					eosErr_ = EdsGetChildAtIndex(eosCameras_, c, &camera);
					if (camera != NULL)
					{
						EdsCloseSession(camera);
						EdsRelease(camera);
						camera = NULL;
					}
				}
			}
			// Release camera list
			EdsRelease(eosCameras_);
			eosCameras_ = NULL;
		}
		EdsTerminateSDK();
	}


String ^CanonCamera::GetImagePath(EdsDirectoryItemRef &dirItem, String ^srcFName)
{
	bool imgFound = false;
	EdsError err = EDS_ERR_OK;
	EdsDirectoryItemInfo dirInfo;
	err = EdsGetDirectoryItemInfo(dirItem, &dirInfo);
	Boolean isFolder = (Boolean)dirInfo.isFolder;
	String ^dirName = Marshal::PtrToStringAnsi(static_cast<System::IntPtr>(dirInfo.szFileName));
	String ^dstName = "";
	if (!isFolder)
	{
		if (dirName == srcFName)
		{
			imgFound = true;
			dstName = dirName;
		}
	}
	else
	{
		EdsUInt32 subDirCount;
		EdsDirectoryItemRef subDirItem;
		err = EdsGetChildCount(dirItem, &subDirCount);
		String ^subName = "";
		for (unsigned int s = 0; s < subDirCount && !imgFound; s++)
		{
			err = EdsGetChildAtIndex(dirItem, s, &subDirItem);
			if (err == EDS_ERR_OK)
			{
				subName = GetImagePath(subDirItem, srcFName);
				if ((subName != nullptr) &&
					(subName->Length > 0))
				{
					dstName = dirName + "\\" + subName;
					imgFound = true;
				}
			}
			EdsRelease(subDirItem);
		}
	}
	return dstName;

}
String ^CanonCamera::GetImagePathFromVolume(EdsVolumeRef volRef, String ^srcFName)
{
	EdsVolumeInfo volInfo;
	EdsGetVolumeInfo(volRef, &volInfo);
	String ^volName = Marshal::PtrToStringAnsi(static_cast<System::IntPtr>(volInfo.szVolumeLabel));
	String ^dstName = "";
	EdsError err = EDS_ERR_OK;
	EdsUInt32 dirCount;
	err = EdsGetChildCount(volRef, &dirCount);
	EdsDirectoryItemRef dirItem;
	bool imgFound = false;
	if (err == EDS_ERR_OK)
	{
		for (unsigned int d = 0; d < dirCount && !imgFound; d++)
		{
			String ^imgName = "";
			err = EdsGetChildAtIndex(volRef, d, &dirItem);
			if (err == EDS_ERR_OK)
			{
				imgName = GetImagePath(dirItem, srcFName);
				if ((imgName != nullptr) &&
					(imgName->Length > 0))
				{
					dstName = volName + "\\" + imgName;
					imgFound = true;
				}
			}
			EdsRelease(dirItem);
		}
	}
	return dstName;
}

String ^CanonCamera::GetImagePathFromCamera(EdsCameraRef camera, 
											EdsDirectoryItemRef &dirItem, 
											String ^srcFName)
{
	EdsUInt32 volCount;
	EdsVolumeRef volRef;
	EdsError err = EDS_ERR_OK;
	err = EdsGetChildCount(camera, &volCount);
	bool imgFound = false;
	String ^imgPath = "";
	if (err == EDS_ERR_OK)
	{
		for (unsigned int v = 0; v < volCount && !imgFound; v++)
		{
			err = EdsGetChildAtIndex(camera, 0, &volRef);
			imgPath = GetImagePathFromVolume(volRef, srcFName);
			EdsRelease(volRef);
			if ((imgPath != nullptr) &&
				(imgPath->Length > 0))
			{
				break;
			}
		}
	}
	return imgPath;
}

//manage the image --- for WaldoAir, we just create a cross-reference to the SD card name and the mission plan name
void CanonCamera::DownloadImage(EdsDirectoryItemRef directoryItem, 
									EdsChar *idxStr)  //idxStr is the ASCI image name on the SD card
{

	int idx = 0;
	if ((idxStr != NULL) && (strlen(idxStr) > 0))
	{
		idx = atoi(idxStr);  //ASCII to integer
	}

	EdsError err = EDS_ERR_OK;
	EdsStreamRef stream = NULL;

	String ^tmpFName = "";
	EdsDirectoryItemInfo dirItemInfo;
	err = EdsGetDirectoryItemInfo(directoryItem, & dirItemInfo);
	if (err != EDS_ERR_OK)
	{
		throw(gcnew Exception("Could not get image info for download. Canon Err Code: " + err.ToString("0")));
	}

	//imgSrc is the image name as entered to the S card : IMG_2026.JPG
	String ^imgSrc = Marshal::PtrToStringAnsi(static_cast<System::IntPtr>(dirItemInfo.szFileName));

	EdsCameraRef camera;
	eosErr_ = EdsGetChildAtIndex(eosCameras_, idx, &camera);

	//imgPath is the image name with path on the SD card  e.g.  SD\DCIM\100CANON\IMG_2026.JPG
	imgFileNameWithPath = GetImagePathFromCamera(camera, directoryItem, imgSrc);

	twLog->WriteLine( imgFileNameWithPath );
	twLog->Flush();

	imageReady_ = true;
 }


	EdsError CanonCamera::handleObjectEvent( EdsObjectEvent evt,
												EdsBaseRef object,
												EdsVoid * context)
	{
		///////////////////////////////////////////////////////////////////////////////
		//this code traps all object events (files changed on SD card) and lsts them 
		///////////////////////////////////////////////////////////////////////////////
		EdsChar path[EDS_MAX_NAME+1];
		if (context != NULL)
		{
			sprintf_s(path, "%s", (EdsChar*)context);  //create a char* 
		}
		else
		{
			sprintf_s(path, ".\\");
		}
		//write the object event to a log  -- cam.txt
		twLog->WriteLine("Object event: " + evt.ToString("X") + " (" + evt.ToString()+ ")" );
		twLog->Flush();

		if (evt == kEdsObjectEvent_DirItemCreated)
		{
			DownloadImage(object, path);
		}

		// Object must be released
		if(object)
		{
			EdsRelease(object);
		}
		return EDS_ERR_OK;
	}



	EdsError CanonCamera::handlePropertyEvent (EdsPropertyEvent evt,
											  EdsPropertyID prop,
											  EdsUInt32 parameter,
											  EdsVoid * context)
	{
		// do something
		switch(prop)
		{
		case kEdsPropID_Evf_OutputDevice:
			{
				/*
				if ((parameter & kEdsEvfOutputDevice_PC) == kEdsEvfOutputDevice_PC)
				{
					// startLiveView;
					liveFeed_ = true;
				}
				else
				{
					liveFeed_ = false;
				}
				*/
			}
		}
		return EDS_ERR_OK;
	}


	EdsError CanonCamera::handleStateEvent (EdsPropertyEvent evt,
										  EdsPropertyID prop,
										  EdsUInt32 parameter,
										  EdsVoid * context)
	{
		switch(prop)
		{
		case kEdsPropID_Evf_OutputDevice:
			{
				if ((parameter & kEdsEvfOutputDevice_PC) == kEdsEvfOutputDevice_PC)
				{
					// startLiveView;
					liveFeed_ = true;
				}
				else
				{
					liveFeed_ = false;
				}
			}
		}
		// do something
		return EDS_ERR_OK;
	}


	void CanonCamera::StartLiveView(void)
	{
		// Get the output device for the live view image
		EdsUInt32 device;

		if (eosCameras_ != NULL)
		{
			EdsUInt32 count;
			eosErr_ = EdsGetChildCount(eosCameras_, &count);
			if ((count > 0) && (eosErr_ == EDS_ERR_OK))
			{
				for (unsigned int c = 0; c < count; c++)
				{
					EdsCameraRef camera;
					eosErr_ = EdsGetChildAtIndex(eosCameras_, c, &camera);
					if (camera != NULL)
					{
						eosErr_ = EdsGetPropertyData(camera, kEdsPropID_Evf_OutputDevice, 0, sizeof(device), &device );
						// PC live view starts by setting the PC as the output device for the live view image.
						if(eosErr_  == EDS_ERR_OK)
						{
							device |= kEdsEvfOutputDevice_PC | kEdsEvfOutputDevice_TFT;
							eosErr_ = EdsSetPropertyData(camera, kEdsPropID_Evf_OutputDevice, 0, sizeof(device), &device);
						}
						// A property change event notification is issued from the camera 
						//  if property settings are made successfully.
						liveFeed_ = true;
						// Start downloading of the live view image once the property 
						//  change notification arrives.
					}
				}
			}
		}
	}

	void CanonCamera::EndLiveView(void)
	{
		// Get the output device for the live view image
		EdsUInt32 device;
		if (eosCameras_ != NULL)
		{
			EdsUInt32 count;
			eosErr_ = EdsGetChildCount(eosCameras_, &count);
			if ((count > 0) && (eosErr_ == EDS_ERR_OK))
			{
				for (unsigned int c = 0; c < count; c++)
				{
					EdsCameraRef camera;
					eosErr_ = EdsGetChildAtIndex(eosCameras_, c, &camera);
					if (camera != NULL)
					{
						eosErr_ = EdsGetPropertyData(camera, kEdsPropID_Evf_OutputDevice, 0, sizeof(device), &device );
						// PC live view ends if the PC is disconnected from the live view image output device.
						if (eosErr_ == EDS_ERR_OK)
						{
							device &= ~kEdsEvfOutputDevice_PC;
							eosErr_ = EdsSetPropertyData(camera, kEdsPropID_Evf_OutputDevice, 0 , sizeof(device), &device);
							liveFeed_ = false;
						}
					}
				}
			}
		}
	}

	void CanonCamera::SetPath(String ^path)
	{
		imgPath_ = path;
		if (imgPath_[imgPath_->Length-1] != '\\')
		{
			imgPath_ += "\\";
		}
		if (!Directory::Exists(imgPath_))
		{
			Directory::CreateDirectory(imgPath_);
		}
		String ^pathInst;
		EdsUInt32 count;
		objDelegate = gcnew handleObjectDelegate(this, &CanonCamera::handleObjectEvent);
		IntPtr objPtr = Marshal::GetFunctionPointerForDelegate(objDelegate);
		propDelegate = gcnew handlePropertyDelegate(this, &CanonCamera::handlePropertyEvent);
		IntPtr propPtr = Marshal::GetFunctionPointerForDelegate(propDelegate);
		stateDelegate = gcnew handleStateDelegate(this, &CanonCamera::handleStateEvent);
		IntPtr statePtr = Marshal::GetFunctionPointerForDelegate(stateDelegate);

		if (eosCameras_ != NULL)
		{
			eosErr_ = EdsGetChildCount(eosCameras_, &count);
			if ((count > 0) && (eosErr_ == EDS_ERR_OK))
			{
				for (unsigned int c = 0; c < count; c++)
				{
					pathInst = imgPath_ + c.ToString("0") + "\\";
					if (!Directory::Exists(pathInst))
					{
						Directory::CreateDirectory(pathInst);
					}
					EdsCameraRef camera;
					eosErr_ = EdsGetChildAtIndex(eosCameras_, c, &camera);
					if (camera != NULL)
					{
						char* str1;
						str1 = (char*)(void*)Marshal::StringToHGlobalAnsi(pathInst);
						eosErr_ = EdsSetObjectEventHandler(camera, kEdsObjectEvent_All,
															(EdsObjectEventHandler)objPtr.ToPointer(), NULL);
						eosErr_ = EdsSetPropertyEventHandler(camera, kEdsPropertyEvent_All,
															(EdsPropertyEventHandler)propPtr.ToPointer(), NULL);
						eosErr_ = EdsSetPropertyEventHandler(camera, kEdsStateEvent_All,
															(EdsPropertyEventHandler)statePtr.ToPointer(), NULL);
					}
				}
			}
		}
	}

	String ^CanonCamera::GetPath(void)
	{
		return imgPath_;
	}

	void CanonCamera::GetCameras()
	{
		//EdsCameraListRef cameraList = NULL;
		EdsUInt32 count = 0;
	
		// Get camera List
		EdsCameraListRef cameraList;
		eosErr_ = EdsGetCameraList(&cameraList);
		eosCameras_ = cameraList;

		if (eosErr_ != EDS_ERR_OK)
		{
			System::Exception ^exc;
			exc = gcnew System::Exception("Could not find camera.");
			throw exc;
		}
		// Get number of cameras
		eosErr_ = EdsGetChildCount(eosCameras_, &count);
		if ((count == 0) || (eosErr_ != EDS_ERR_OK))
		{
			eosErr_ = EDS_ERR_DEVICE_NOT_FOUND;
			// Release camera list
			if (eosCameras_ != NULL)
			{
				EdsRelease(eosCameras_);
				eosCameras_ = NULL;
			}
			System::Exception ^exc;
			exc = gcnew System::Exception("Could not find camera.");
			throw exc;
		}
	}


	bool CanonCamera::GetLiveFeedImage(int idx, System::Drawing::Bitmap ^%bmp)
	{
		if (!liveFeed_)
		{
			return liveFeed_;
		}
		if (eosCameras_ == NULL)
		{
			return false;
		}
		EdsUInt32 count = 0;
		eosErr_ = EdsGetChildCount(eosCameras_, &count);
		if (((int)count <= idx) || (eosErr_ != EDS_ERR_OK))
		{
			return false;
		}
		EdsCameraRef camera;
		if(eosErr_ == EDS_ERR_OK)
		{
			eosErr_ = EdsGetChildAtIndex(eosCameras_, idx, &camera);
		}
		EdsStreamRef stream = NULL;
		EdsEvfImageRef evfImage = NULL;
		// Create memory stream.
		if(eosErr_ == EDS_ERR_OK)
		{
			eosErr_ = EdsCreateMemoryStream( 0, &stream);
		}
		// Create EvfImageRef.
		if(eosErr_ == EDS_ERR_OK)
		{
			eosErr_ = EdsCreateEvfImageRef(stream, &evfImage);
		}
		// Download live view image data.
		if(eosErr_ == EDS_ERR_OK)
		{
			eosErr_ = EdsDownloadEvfImage(camera, evfImage);
		}
		unsigned char *ipData;
		eosErr_ = EdsGetPointer(stream, (EdsVoid **)&ipData);
		EdsUInt32 sLen;
		if(eosErr_ == EDS_ERR_OK)
		{
			eosErr_ = EdsGetLength(stream, &sLen);
		}

		array<Byte> ^buffer = gcnew array<Byte>(sLen);
		// Get the incidental data of the image.
	
		EdsUInt32 zoomScale;
		if(eosErr_ == EDS_ERR_OK)
		{
			eosErr_ = EdsGetPropertyData(evfImage, kEdsPropID_Evf_Zoom, 0, sizeof(zoomScale), &zoomScale);
		}
	
		EdsRect rect;
		if(eosErr_ == EDS_ERR_OK)
		{
			eosErr_ = EdsGetPropertyData(evfImage, kEdsPropID_Evf_ZoomRect, 0, sizeof(rect), &rect);
		}

		EdsPoint pos;
		if(eosErr_ == EDS_ERR_OK)
		{
			eosErr_ = EdsGetPropertyData(evfImage, kEdsPropID_Evf_ImagePosition, 0 , sizeof(pos), &pos);	
		}
		if(eosErr_ == EDS_ERR_OK)
		{
			int basePosX = 0;
			if (pos.x != basePosX)
			{
				pos.x = basePosX;
				eosErr_ = EdsSetPropertyData(camera, kEdsPropID_Evf_ZoomPosition, 0 , sizeof(pos), &pos);	
			}
		}
		if(eosErr_ == EDS_ERR_OK)
		{
			int basePosY = 0;
			if (pos.y != basePosY)
			{
				pos.y = basePosY;
				eosErr_ = EdsSetPropertyData(camera, kEdsPropID_Evf_ZoomPosition, 0 , sizeof(pos), &pos);	
			}
		}
		if(eosErr_ == EDS_ERR_OK)
		{
			Marshal::Copy((IntPtr)ipData, buffer, 0, (int)sLen);
			MemoryStream ^memStream = gcnew MemoryStream(buffer);
			bmp = (Bitmap^)Bitmap::FromStream(memStream);
			int width = bmp->Width;
			int height = bmp->Height;
			Imaging::BitmapData ^bmpData = bmp->LockBits(
									Drawing::Rectangle(0, 0, width, height),
									Imaging::ImageLockMode::ReadWrite,
									Imaging::PixelFormat::Format24bppRgb);
			int xBase, xDest;
			int yBase, yDest;
			array<Byte> ^ucBuf = gcnew array<Byte>(width*height*3);
			unsigned char *ucPtr = (unsigned char *)bmpData->Scan0.ToPointer();
			int h, w;
			try
			{
				for (h = 0; h < height; h++)
				{
					yBase = h*width;
					yDest = h*width;
					for (w = 0; w < width; w++)
					{
						xBase = w;
						xDest = w;
						for (int b = 0; b < 3; b++)
						{
							ucBuf[(yDest + xDest)*3 + b] =
									ucPtr[(yBase + xBase)*3 + b];
						}
					}
				}
			}
			catch(...)
			{
				;
			}
			Runtime::InteropServices::Marshal::Copy(ucBuf, 0, bmpData->Scan0,width*height*3);
			bmp->UnlockBits(bmpData);

			delete memStream;
		}
		//
		// Display image
		//
		// Release stream
		if(stream != NULL)
		{
			EdsRelease(stream);
			stream = NULL;
		}
		// Release evfImage
		if(evfImage != NULL)
		{
			EdsRelease(evfImage);
			evfImage = NULL;
		}
		if (eosErr_ == EDS_ERR_OK)
		{
			return liveFeed_;
		}
		else
		{
			return false;
		}
	}

	//imageReady called by polling loop from parent to determine when an image was fired
	//returns a filename with path and "true" to indicate to indicate the fire
	//resetImageReady() procedure below must be called to reset the imageReady flag
	bool CanonCamera::ImageReady( [Out] String^% imageFilenamne)
	{
		imageFilenamne = imgFileNameWithPath;
		return imageReady_;
	}

	//reset the imageReady flag
	void CanonCamera::resetImageReady(void)
	{
		imageReady_ = false;
	}

	void CanonCamera::FireTrigger()  //assumes may be multiple cameras (trigger them all)
	{
		EdsUInt32 count;

		//dont allow trigger to fire too fast
		if (triggerFired_ && ((DateTime::Now - triggerTime_).TotalSeconds < 0.5))
		{
			return;
		}

		imageReady_ = false;
		if (eosCameras_ != NULL)
		{
			eosErr_ = EdsGetChildCount(eosCameras_, &count);  //number of attached cameras
			if ((count > 0) && (eosErr_ == EDS_ERR_OK))
			{
				for (unsigned int c = 0; c < count; c++)
				{
					EdsCameraRef camera;
					eosErr_ = EdsGetChildAtIndex(eosCameras_, c, &camera);
					if (camera != NULL)
					{
						//this is the command to take a picture using the digital interface
						eosErr_ = ::EdsSendCommand(camera, 
													kEdsCameraCommand_TakePicture, 
													0);
						if (eosErr_ != EDS_ERR_OK)
						{
							// report error
							return;
						}
						triggerFired_ = true;
						triggerTime_ = DateTime::Now;
					}
				}
			}
		}
	}

	void CanonCamera::ListVolume(void)
	{
		if (eosCameras_ != NULL)
		{
			EdsUInt32 count;
			eosErr_ = EdsGetChildCount(eosCameras_, &count);
			if ((count > 0) && (eosErr_ == EDS_ERR_OK))
			{
				for (unsigned int c = 0; c < count; c++)
				{
					EdsCameraRef camera;
					eosErr_ = EdsGetChildAtIndex(eosCameras_, c, &camera);
					if (camera != NULL)
					{
						ListVolumes(camera);
					}
				}
			}
		}
	}

	void CanonCamera::ListVolumes(EdsCameraRef &camera)
	{
		EdsUInt32 volCount;
		EdsVolumeRef volRef;
		EdsVolumeInfo volInfo;
		EdsError err = EDS_ERR_OK;
		err = EdsGetChildCount(camera, &volCount);
		bool imgFound = false;
		if (err == EDS_ERR_OK)
		{
			for (unsigned int v = 0; v < volCount && !imgFound; v++)
			{
				err = EdsGetChildAtIndex(camera, v, &volRef);
				if (err != EDS_ERR_OK)
				{
					EdsRelease(volRef);
					continue;
				}
				err = EdsGetVolumeInfo(volRef, &volInfo);
				if (err != EDS_ERR_OK)
				{
					EdsRelease(volRef);
					continue;
				}
				String ^volName = Marshal::PtrToStringAnsi(static_cast<System::IntPtr>(volInfo.szVolumeLabel));
				twLog->WriteLine("Found Volume : " + volName);
				twLog->Flush();
				ListVolume(volRef);
				EdsRelease(volRef);
			}
		}
	}

	void CanonCamera::ListVolume(EdsVolumeRef  &volRef)
	{
		EdsUInt32 dirCount;
		EdsDirectoryItemRef dirRef;
		EdsDirectoryItemInfo dirInfo;
		EdsError err = EDS_ERR_OK;
		err = EdsGetChildCount(volRef, &dirCount);
		bool imgFound = false;
		if (err == EDS_ERR_OK)
		{
			for (unsigned int d = 0; d < dirCount && !imgFound; d++)
			{
				err = EdsGetChildAtIndex(volRef, d, &dirRef);
				if (err != EDS_ERR_OK)
				{
					EdsRelease(dirRef);
					continue;
				}
				err = EdsGetDirectoryItemInfo(dirRef, &dirInfo);
				if (err != EDS_ERR_OK)
				{
					EdsRelease(dirRef);
					continue;
				}
				String ^dirName = Marshal::PtrToStringAnsi(static_cast<System::IntPtr>(dirInfo.szFileName));
				if (!dirInfo.isFolder)
				{
					twLog->WriteLine("Found Item : " + dirName);
				}
				else
				{
					twLog->WriteLine("Found Directory : " + dirName);
					ListDirectory(dirRef);
				}
				twLog->Flush();
				EdsRelease(dirRef);
			}
		}
	}

	void CanonCamera::ListDirectory(EdsDirectoryItemRef &dirRef)
	{
		EdsUInt32 dirCount;
		EdsDirectoryItemRef dirItemRef;
		EdsDirectoryItemInfo dirItemInfo;
		EdsError err = EDS_ERR_OK;
		err = EdsGetChildCount(dirRef, &dirCount);
		bool imgFound = false;
		if (err == EDS_ERR_OK)
		{
			for (unsigned int d = 0; d < dirCount && !imgFound; d++)
			{
				err = EdsGetChildAtIndex(dirRef, d, &dirItemRef);
				if (err != EDS_ERR_OK)
				{
					EdsRelease(dirItemRef);
					continue;
				}
				err = EdsGetDirectoryItemInfo(dirItemRef, &dirItemInfo);
				if (err != EDS_ERR_OK)
				{
					EdsRelease(dirItemRef);
					continue;
				}
				String ^dirName = Marshal::PtrToStringAnsi(static_cast<System::IntPtr>(dirItemInfo.szFileName));
				if (!dirItemInfo.isFolder)
				{
					twLog->WriteLine("Found Item : " + dirName);
				}
				else
				{
					twLog->WriteLine("Found Directory : " + dirName);
					ListDirectory(dirItemRef);
				}
				twLog->Flush();
				EdsRelease(dirItemRef);
			}
		}
	}

	void CanonCamera::SetShutters(double shutter)  //assumes multiple cameras
	{
		EdsUInt32 count;

		eosErr_ = EdsGetChildCount(eosCameras_, &count);  //get number of attached cameras

		for (unsigned int idx = 0; idx < count; idx++)
		{
			try
			{
				SetShutter(shutter, idx);
			}
			catch(Exception ^exc)
			{
				twLog->WriteLine(exc->ToString());
				twLog->Flush();
				throw exc;
			}
		}
	}

	//set the canon camera shutter
	void CanonCamera::SetShutter(double shutter, int idx)
	{
		EdsUInt32 shutterCode;  
		//get the Canon shutter code that corresponds to the requested shutter intervall in msecs
		shutterCode = LookupShutterCode(shutter/1000.0);

		twLog->WriteLine("Set Shutter Code: " + shutterCode.ToString("X"));
		twLog->Flush();

		EdsCameraRef camera;
		eosErr_ = EdsGetChildAtIndex(eosCameras_, idx, &camera);
		if (eosErr_ != EDS_ERR_OK)
		{
			System::Exception ^exc;
			exc = gcnew System::Exception("Could not connect to camera at index " + idx.ToString());
			throw exc;
		}

		//actual call to set the canon shutter property 
		eosErr_ = EdsSetPropertyData(camera, kEdsPropID_Tv, 0, sizeof(shutterCode), (void *)&shutterCode);
		if (eosErr_ != EDS_ERR_OK)
		{
			System::Exception ^exc;
			exc = gcnew System::Exception("Could not set shutter for child " + idx.ToString() + ". Error Code : " + eosErr_.ToString());
			throw exc;
		}
	}

	EdsUInt32
	CanonCamera::LookupShutterCode(double shutter)
	{
		EdsUInt32 shutterCode;
		if (shutter >= 0.25)
		{
			if (shutter < 0.29)
			{
				shutterCode = 0x48;
			}
			else if (shutter < 0.35)
			{
				shutterCode = 0x44;
			}
			else if (shutter < 0.45)
			{
				shutterCode = 0x43;
			}
			else if (shutter < 0.55)
			{
				shutterCode = 0x40;
			}
			else if (shutter < 0.65)
			{
				shutterCode = 0x3D;
			}
			else if (shutter < 0.75)
			{
				shutterCode = 0x3C;
			}
			else if (shutter < 0.9)
			{
				shutterCode = 0x3B;
			}
			else if (shutter < 1.167)
			{
				shutterCode = 0x38;
			}
			else if (shutter < 1.4)
			{
				shutterCode = 0x35;
			}
			else if (shutter < 1.55)
			{
				shutterCode = 0x34;
			}
			else if (shutter < 1.8)
			{
				shutterCode = 0x33;
			}
			else if (shutter < 2.25)
			{
				shutterCode = 0x30;
			}
			else if (shutter < 2.75)
			{
				shutterCode = 0x2D;
			}
			else if (shutter < 3.1)
			{
				shutterCode = 0x2C;
			}
			else if (shutter < 3.6)
			{
				shutterCode = 0x2B;
			}
			else if (shutter < 4.5)
			{
				shutterCode = 0x28;
			}
			else if (shutter < 5.5)
			{
				shutterCode = 0x25;
			}
			else if (shutter < 7)
			{
				shutterCode = 0x24;
			}
			else if (shutter < 9)
			{
				shutterCode = 0x20;
			}
			else if (shutter < 11.5)
			{
				shutterCode = 0x1C;
			}
			else if (shutter < 14)
			{
				shutterCode = 0x1B;
			}
			else if (shutter < 17.5)
			{
				shutterCode = 0x18;
			}
			else if (shutter < 22.5)
			{
				shutterCode = 0x14;
			}
			else if (shutter < 27.5)
			{
				shutterCode = 0x13;
			}
			else
			{
				shutterCode = 0x10;
			}
		}
		else
		{
			double shutterInv = 1.0/shutter;
			if (shutterInv < 4.5)
			{
				shutterCode = 0x48;
			}
			else if (shutterInv < 5.5)
			{
				shutterCode = 0x4B;
			}
			else if (shutterInv < 7)
			{
				shutterCode = 0x4C;
			}
			else if (shutterInv < 9)
			{
				shutterCode = 0x50;
			}
			else if (shutterInv < 11.5)
			{
				shutterCode = 0x54;
			}
			else if (shutterInv < 14)
			{
				shutterCode = 0x55;
			}
			else if (shutterInv < 17.5)
			{
				shutterCode = 0x58;
			}
			else if (shutterInv < 22.5)
			{
				shutterCode = 0x5C;
			}
			else if (shutterInv < 27.5)
			{
				shutterCode = 0x5D;
			}
			else if (shutterInv < 35)
			{
				shutterCode = 0x60;
			}
			else if (shutterInv < 42.5)
			{
				shutterCode = 0x63;
			}
			else if (shutterInv < 47.5)
			{
				shutterCode = 0x64;
			}
			else if (shutterInv < 55)
			{
				shutterCode = 0x65;
			}
			else if (shutterInv < 70)
			{
				shutterCode = 0x68;
			}
			else if (shutterInv < 85)
			{
				shutterCode = 0x6B;
			}
			else if (shutterInv < 95)
			{
				shutterCode = 0x6C;
			}
			else if (shutterInv < 112.5)
			{
				shutterCode = 0x6D;
			}
			else if (shutterInv < 142.5)
			{
				shutterCode = 0x70;
			}
			else if (shutterInv < 170)
			{
				shutterCode = 0x73;
			}
			else if (shutterInv < 190)
			{
				shutterCode = 0x74;
			}
			else if (shutterInv < 225)
			{
				shutterCode = 0x75;
			}
			else if (shutterInv < 285)
			{
				shutterCode = 0x78;
			}
			else if (shutterInv < 335)
			{
				shutterCode = 0x7B;
			}
			else if (shutterInv < 375)
			{
				shutterCode = 0x7C;
			}
			else if (shutterInv < 450)
			{
				shutterCode = 0x7D;
			}
			else if (shutterInv < 570)
			{
				shutterCode = 0x80;
			}
			else if (shutterInv < 695)
			{
				shutterCode = 0x83;
			}
			else if (shutterInv < 775)
			{
				shutterCode = 0x84;
			}
			else if (shutterInv < 900)
			{
				shutterCode = 0x85;
			}
			else if (shutterInv < 1125)
			{
				shutterCode = 0x88;
			}
			else if (shutterInv < 1375)
			{
				shutterCode = 0x8B;
			}
			else if (shutterInv < 1550)
			{
				shutterCode = 0x8C;
			}
			else if (shutterInv < 1800)
			{
				shutterCode = 0x8D;
			}
			else if (shutterInv < 2250)
			{
				shutterCode = 0x90;
			}
			else if (shutterInv < 2750)
			{
				shutterCode = 0x93;
			}
			else if (shutterInv < 3100)
			{
				shutterCode = 0x94;
			}
			else if (shutterInv < 3600)
			{
				shutterCode = 0x95;
			}
			else if (shutterInv < 4500)
			{
				shutterCode = 0x98;
			}
			else if (shutterInv < 5500)
			{
				shutterCode = 0x9B;
			}
			else if (shutterInv < 6200)
			{
				shutterCode = 0x9C;
			}
			else if (shutterInv < 7200)
			{
				shutterCode = 0x9D;
			}
			else
			{
				shutterCode = 0xA0;
			}
		}
		return shutterCode;
	}

	void
	CanonCamera::GetISO(int &isoSpeed, int idx)
	{
		EdsUInt32 isoCode;
		EdsError err = EDS_ERR_OK;
		EdsUInt32 dataType;
		EdsUInt32 dataSize;

		EdsCameraRef camera;
		eosErr_ = EdsGetChildAtIndex(eosCameras_, idx, &camera);
		if (eosErr_ != EDS_ERR_OK)
		{
			System::Exception ^exc;
			exc = gcnew System::Exception("Could not connect to camera at index " + idx.ToString());
			throw exc;
		}
		eosErr_ = EdsGetPropertySize(camera, kEdsPropID_ISOSpeed, 0 , (EdsDataType*)&dataType, &dataSize);
		if(eosErr_ == EDS_ERR_OK)
		{
			eosErr_ = EdsGetPropertyData(camera, kEdsPropID_ISOSpeed, 0 , dataSize, &isoCode);
		}
		isoSpeed = GetISOFromCode(isoCode);
	}

	void
	CanonCamera::SetISOs(int isoSpeed)
	{
		EdsUInt32 count;
		eosErr_ = EdsGetChildCount(eosCameras_, &count);
		for (unsigned int idx = 0; idx < count; idx++)
		{
			try
			{
				SetISO(isoSpeed, idx);
			}
			catch(Exception ^exc)
			{
				twLog->WriteLine(exc->ToString());
				twLog->Flush();
				throw exc;
			}
		}
	}

	void
	CanonCamera::SetISO(int isoSpeed, int idx)
	{
		EdsUInt32 isoCode;
		isoCode = LookupISOCode(isoSpeed);

		twLog->WriteLine("Set ISO Code: " + isoCode.ToString("X"));
		twLog->Flush();
		EdsCameraRef camera;
		eosErr_ = EdsGetChildAtIndex(eosCameras_, idx, &camera);
		if (eosErr_ != EDS_ERR_OK)
		{
			System::Exception ^exc;
			exc = gcnew System::Exception("Could not connect to camera at index " + idx.ToString());
			throw exc;
		}
		eosErr_ = EdsSetPropertyData(camera, kEdsPropID_ISOSpeed, 0, sizeof(isoCode), (void *)&isoCode);
		if (eosErr_ != EDS_ERR_OK)
		{
			System::Exception ^exc;
			exc = gcnew System::Exception("Could not set ISO speed for child " + idx.ToString() + ". Error Code : " + eosErr_.ToString());
			throw exc;
		}
		try
		{
			int isoSpeedSet;
			GetISO(isoSpeedSet, idx);
			twLog->WriteLine("ISO Speed Set : " + isoSpeedSet.ToString());
			twLog->Flush();
			/*
			if (isoSpeedSet < isoSpeed)
			{
				maxISOLimit_ = isoSpeedSet;
			}
			*/
		}
		catch(...)
		{
			// just testing value
		}
	}

	EdsUInt32
	CanonCamera::LookupISOCode(int isoSpeed)
	{
		EdsUInt32 isoCode;
		switch (isoSpeed)
		{
			case 100:
				isoCode = 0x048;
			break;
			case 125:
				isoCode = 0x04b;
			break;
			case 160:
				isoCode = 0x04d;
			break;
			case 200:
				isoCode = 0x050;
			break;
			case 250:
				isoCode = 0x053;
			break;
			case 320:
				isoCode = 0x055;
			break;
			case 400:
				isoCode = 0x058;
			break;
			case 500:
				isoCode = 0x05b;
			break;
			case 640:
				isoCode = 0x05d;
			break;
			case 800:
				isoCode = 0x060;
			break;
			case 1000:
				isoCode = 0x063;
			break;
			case 1250:
				isoCode = 0x065;
			break;
			case 1600:
				isoCode = 0x068;
			break;
			case 2000:
				isoCode = 0x06b;
			break;
			case 2500:
				isoCode = 0x06d;
			break;
			case 3200:
				isoCode = 0x070;
			break;
			case 4000:
				isoCode = 0x073;
			break;
			case 5000:
				isoCode = 0x075;
			break;
			case 6400:
				isoCode = 0x078;
			break;
		}
		return isoCode;
	}

	int
	CanonCamera::GetISOFromCode(EdsUInt32 isoCode)
	{
		int isoSpeed;
		switch(isoCode)
		{
			case 0x048:
				isoCode = 100;
			break;
			case 0x04b:
				isoCode = 125;
			break;
			case 0x04d:
				isoCode = 160;
			break;
			case 0x050:
				isoCode = 200;
			break;
			case 0x053:
				isoCode = 250;
			break;
			case 0x055:
				isoCode = 320;
			break;
			case 0x058:
				isoCode = 400;
			break;
			case 0x05b:
				isoCode = 500;
			break;
			case 0x05d:
				isoCode = 640;
			break;
			case 0x060:
				isoCode = 800;
			break;
			case 0x063:
				isoCode = 1000;
			break;
			case 0x065:
				isoCode = 1250;
			break;
			case 0x068:
				isoCode = 1600;
			break;
			case 0x06b:
				isoCode = 2000;
			break;
			case 0x06d:
				isoCode = 2500;
			break;
			case 0x070:
				isoCode = 3200;
			break;
			case 0x073:
				isoCode = 4000;
			break;
			case 0x075:
				isoCode = 5000;
			break;
			case 0x078:
				isoCode = 6400;
			break;
			default:
				isoSpeed = 0;
			break;
		}
		return isoSpeed;
	}

}


