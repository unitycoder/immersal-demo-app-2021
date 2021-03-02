/*===============================================================================
Copyright (C) 2020 Immersal Ltd. All Rights Reserved.

This file is part of the Immersal SDK.

The Immersal SDK cannot be copied, distributed, or made available to
third-parties for commercial purposes without written permission of Immersal Ltd.

Contact sdk@immersal.com for licensing requests.
===============================================================================*/

using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEngine;
using Immersal.AR;
using Immersal.REST;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Immersal.Samples.DemoApp
{
    public class AutomaticMapper : MonoBehaviour
    {
        public delegate void OnMapSubmitted();
        public static event OnMapSubmitted onMapSubmitted;
        public delegate void OnImageUploaded();
        public static event OnImageUploaded onImageUploaded;

        private bool m_bCaptureRunning = false;
        private uint m_ImageRun = 0;
        private int m_ImageIndex = 0;
        private bool m_RgbCapture = false;

        private ImmersalSDK m_Sdk = null;
        private List<Task> m_Jobs = new List<Task>();
        private int m_JobLock = 0;

        private Camera m_MainCamera = null;

        void Start()
        {
            m_Sdk = ImmersalSDK.Instance;
            m_MainCamera = Camera.main;

            DirectoryInfo dataDir = new DirectoryInfo(tempImagePath);
            if (dataDir.Exists)
            {
                dataDir.Delete(true);
            }

            Directory.CreateDirectory(tempImagePath);

#if UNITY_IOS
            UnityEngine.iOS.Device.SetNoBackupFlag(tempImagePath);
#endif
        }

        private void Update()
        {
            if (m_JobLock == 1)
                return;
            if (m_Jobs.Count > 0)
            {
                m_JobLock = 1;
                RunJob(m_Jobs[0]);
            }
        }

        public string tempImagePath
        {
            get
            {
                return string.Format("{0}/Images", Application.persistentDataPath);
            }
        }

        public void ResetMapperPictures(bool deleteAnchor)
        {
            JobClearAsync j = new JobClearAsync();
            j.anchor = deleteAnchor;
            j.OnResult += (SDKClearResult result) =>
            {
                Debug.Log("Workspace cleared successfully");
            };

            m_Jobs.Add(j.RunJobAsync());
        }

        public async void Capture()
        {
            await Task.Delay(250);

            m_bCaptureRunning = true;
            float captureStartTime = Time.realtimeSinceStartup;
            float uploadStartTime = Time.realtimeSinceStartup;

            XRCpuImage image;
            ARCameraManager cameraManager = m_Sdk.cameraManager;
            var cameraSubsystem = cameraManager.subsystem;

            if (cameraSubsystem != null && cameraSubsystem.TryAcquireLatestCpuImage(out image))
            {
                JobCaptureAsync j = new JobCaptureAsync();
                j.run = (int)(m_ImageRun & 0xEFFFFFFF);
                j.index = m_ImageIndex++;
                j.anchor = false;

                if (LocationProvider.Instance.gpsOn)
                {
                    j.latitude = LocationProvider.Instance.latitude;
                    j.longitude = LocationProvider.Instance.longitude;
                    j.altitude = LocationProvider.Instance.altitude;
                }
                else
                {
                    j.latitude = j.longitude = j.altitude = 0.0;
                }

                Quaternion _q = m_MainCamera.transform.rotation;
                Matrix4x4 rot = Matrix4x4.Rotate(new Quaternion(_q.x, _q.y, -_q.z, -_q.w));
                Vector3 _p = m_MainCamera.transform.position;
                Vector3 pos = new Vector3(_p.x, _p.y, -_p.z);
                j.rotation = rot;
                j.position = pos;

                ARHelper.GetIntrinsics(out j.intrinsics);

                int width = image.width;
                int height = image.height;

                byte[] pixels;
                int channels = 1;

                if (m_RgbCapture)
                {
                    ARHelper.GetPlaneDataRGB(out pixels, image);
                    channels = 3;
                }
                else
                {
                    ARHelper.GetPlaneData(out pixels, image);
                }

                byte[] capture = new byte[channels * width * height + 1024];

                Task<icvCaptureInfo> captureTask = Task.Run(() =>
                {
                    return Core.CaptureImage(capture, capture.Length, pixels, width, height, channels);
                });

                await captureTask;

                string path = string.Format("{0}/{1}", tempImagePath, System.Guid.NewGuid());
                using (BinaryWriter writer = new BinaryWriter(File.OpenWrite(path)))
                {
                    writer.Write(capture, 0, captureTask.Result.captureSize);
                }

                j.imagePath = path;
                j.encodedImage = "";

                j.OnStart += () =>
                {
                    uploadStartTime = Time.realtimeSinceStartup;
                };
                j.OnResult += (SDKImageResult result) =>
                {
                    float et = Time.realtimeSinceStartup - uploadStartTime;
                    Debug.Log(string.Format("Image uploaded successfully in {0} seconds", et));
                    onImageUploaded?.Invoke();
                };
                j.Progress.ProgressChanged += (s, progress) =>
                {
                    int value = (int)(100f * progress);
                    Debug.Log(string.Format("Upload progress: {0}%", value));
                };
                j.OnError += (e) =>
                {
                    Debug.Log(string.Format("Capture error: " + e));
                };

                m_Jobs.Add(j.RunJobAsync());
                image.Dispose();

                float elapsedTime = Time.realtimeSinceStartup - captureStartTime;
                Debug.Log(string.Format("Capture in {0} seconds", elapsedTime));
            }

            m_bCaptureRunning = false;
        }

        public void Construct(string mapName, bool preservePoses, bool isPublic)
        {
            JobConstructAsync j = new JobConstructAsync();
            j.name = mapName;
            j.featureCount = 600;
            j.preservePoses = preservePoses;
            j.windowSize = 0;
            j.OnResult += (SDKConstructResult result) =>
            {
                Debug.Log(string.Format("Started constructing a map width ID {0}, containing {1} images and detail level of {2}", result.id, result.size, j.featureCount));

                onMapSubmitted?.Invoke();

                if (isPublic)
                {
                    SetSharingMode(result.id, true);
                }
            };

            m_Jobs.Add(j.RunJobAsync());
        }

        public void SetSharingMode(int mapId, bool isPublic)
        {
            JobSetPrivacyAsync j = new JobSetPrivacyAsync();
            j.id = mapId;
            j.privacy = isPublic ? 1 : 0;
            j.OnResult += (SDKMapPrivacyResult result) =>
            {
                Debug.Log(string.Format("Sharing mode set successfully, set to: {0}", j.privacy));
            };

            m_Jobs.Add(j.RunJobAsync());
        }

        private async void RunJob(Task t)
        {
            await t;

            if (m_Jobs.Count > 0)
            {
                m_Jobs.RemoveAt(0);
            }
            m_JobLock = 0;
        }

        internal void ImageRunUpdate()
        {
            long bin = System.DateTime.Now.ToBinary();
            uint data = (uint)bin ^ (uint)(bin >> 32);
            m_ImageRun = (m_ImageRun ^ data) * 16777619;
        }
    }
}
