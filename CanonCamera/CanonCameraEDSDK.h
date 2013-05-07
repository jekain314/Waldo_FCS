// CanonCamera.h

#pragma once

#include "EDSDK.h"

using namespace System;
using namespace System ::Runtime::InteropServices;

namespace CanonCameraEDSDK {

	public ref class CanonCamera
	{

	public:

		CanonCamera(void);
		~CanonCamera(void);
		void FireTrigger(void);
		bool GetLiveFeedImage(int idx, System::Drawing::Bitmap ^%bmp);
		bool ImageReady ( [Out] String^%  imageFilenamne);
		void resetImageReady(); 
		void StartLiveView(void);
		void EndLiveView(void);
		void SetPath(System::String ^path);
		String ^GetPath(void);
		void ListVolume(void);
		void SetShutters(double shutter);
		void GetISO(int &isoSpeed, int idx);
		void SetISOs(int isoSpeed);
		//two procedures below used in a polling loop to return the image name on the camera SD card

	private:

		delegate EdsError handleObjectDelegate( EdsObjectEvent evt,
												EdsBaseRef object,
												EdsVoid * context);
		delegate EdsError handlePropertyDelegate( EdsPropertyEvent evt,
													EdsPropertyID prop,
													EdsUInt32 parameter,
													EdsVoid * context);
		delegate EdsError handleStateDelegate( EdsPropertyEvent evt,
													EdsPropertyID prop,
													EdsUInt32 parameter,
													EdsVoid * context);

		EdsObjectEventHandler objHandler;
		handleObjectDelegate ^objDelegate;
		EdsPropertyEventHandler propHandler;
		handlePropertyDelegate ^propDelegate;
		EdsPropertyEventHandler stateHandler;
		handleStateDelegate ^stateDelegate;

		System::String ^GetImagePath(EdsDirectoryItemRef &dirItem, 
									System::String ^srcFName);
		System::String ^GetImagePathFromVolume(EdsVolumeRef volRef, 
											System::String ^srcFName);
		System::String ^GetImagePathFromCamera(EdsCameraRef camera, 
											EdsDirectoryItemRef &dirItem, 
											System::String ^srcFName);

		void DownloadImage(EdsDirectoryItemRef directoryItem, EdsChar *path);
		void GetCameras();
		void ListVolumes(EdsCameraRef &camera);
		void ListVolume(EdsVolumeRef &volRef);
		void ListDirectory(EdsDirectoryItemRef &dirRef);
		EdsError handleObjectEvent( EdsObjectEvent evt,
							EdsBaseRef object,
							EdsVoid * context);
		EdsError handlePropertyEvent( EdsPropertyEvent evt,
										EdsPropertyID prop,
										EdsUInt32 parameter,
										EdsVoid * context);
		EdsError handleStateEvent( EdsPropertyEvent evt,
										EdsPropertyID prop,
										EdsUInt32 parameter,
										EdsVoid * context);
		void SetShutter(double shutter, int idx);
		EdsUInt32 LookupShutterCode(double shutter);

		void SetISO(int isoSpeed, int idx);
		EdsUInt32 LookupISOCode(int isoSpeed);
		int GetISOFromCode(EdsUInt32 isoCode);

		bool triggerFired_;
		bool imageReady_;
		bool liveFeed_;
		System::DateTime triggerTime_;
		System::String ^imgPath_;
		String^ imgFileNameWithPath;
		EdsCameraListRef eosCameras_;
		EdsError eosErr_;
		System::IO::TextWriter ^twLog;
	};

}


