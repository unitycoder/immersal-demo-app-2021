/*===============================================================================
Copyright (C) 2020 Immersal Ltd. All Rights Reserved.

This file is part of the Immersal SDK.

The Immersal SDK cannot be copied, distributed, or made available to
third-parties for commercial purposes without written permission of Immersal Ltd.

Contact sdk@immersal.com for licensing requests.
===============================================================================*/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using TMPro;
using Immersal.Samples.Util;
using Immersal.Samples.Mapping;

namespace Immersal.Samples.DemoApp
{
    public struct ARPoint
    {
        public float x;
        public float y;
        public float z;
        public ARPoint(Vector3 pos)
        {
            x = pos.x;
            y = pos.y;
            z = pos.z;
        }
    }

    public class AutomaticMapperManager : MonoBehaviour
    {
        public const int MAX_VERTICES = 65535;
        public ARPointCloudManager m_ArPointCloudManager = null;

        [SerializeField]
        private int m_MaxImages = 40;
        [SerializeField]
        private int m_MinImages = 10;
        [SerializeField]
        private float m_ImageInterval = 0.6f;
        [SerializeField]
        private Color m_PointColor = new Color(0.57f, 0.93f, 0.12f);
        [SerializeField]
        private float m_PointSize = 0.05f;
        [SerializeField]
        private bool m_PreservePoses = false;

        [SerializeField]
        private TextMeshProUGUI m_CaptureButtonText = null;
        [SerializeField]
        private Image m_CaptureButtonIcon = null;
        [SerializeField]
        private Sprite m_StartCaptureSprite = null;
        [SerializeField]
        private Sprite m_CancelCaptureSprite = null;
        [SerializeField]
        private Sprite m_StopCaptureSprite = null;

        [SerializeField]
        private CanvasGroup m_ProgressBarCanvasGroup = null;
        [SerializeField]
        private Image m_ForegroundImage = null;
        [SerializeField]
        private TextMeshProUGUI m_ProgressBarText = null;

        [SerializeField]
        private GameObject m_ConstructPrompt = null;
        [SerializeField]
        private TMP_InputField m_ConstructPromptMapNameInputField = null;
        [SerializeField]
        private Toggle m_PublicMapToggle = null;

        [SerializeField]
        private GameObject m_SwitchModePrompt = null;

        [SerializeField]
        private CanvasGroup m_MoveDevicePromptCanvasGroup = null;

        private bool m_IsMapping = false;
        private int m_ImagesSubmitted = 0;
        private int m_ImagesUploaded = 0;
        private bool m_WaitForUpload = false;
        private Camera m_MainCamera = null;
        private bool m_CameraHasMoved = false;
        private Vector3 camPrevPos = Vector3.zero;
        private Quaternion camPrevRot = Quaternion.identity;

        private GameObject m_PointCloud = null;
        private Mesh m_Mesh = null;
        private MeshFilter m_MeshFilter = null;
        private MeshRenderer m_MeshRenderer = null;
        private List<ARPoint> m_Points = new List<ARPoint>();
        private List<ARPoint> m_CurrentPoints = new List<ARPoint>();

        private AutomaticMapper m_AutomaticMapper = null;

        void Start()
        {
            InitMesh();
            m_MainCamera = Camera.main;
            camPrevPos = m_MainCamera.transform.position;
            camPrevRot = m_MainCamera.transform.rotation;
            m_ProgressBarCanvasGroup.gameObject.SetActive(false);
            m_MoveDevicePromptCanvasGroup.gameObject.SetActive(false);
            m_AutomaticMapper = GetComponent<AutomaticMapper>();
            m_ConstructPrompt.SetActive(false);
            m_SwitchModePrompt.SetActive(false);
        }

        private void OnEnable()
        {
            m_ArPointCloudManager.pointCloudsChanged += PointCloudManager_pointCloudsChanged;
            AutomaticMapper.onMapSubmitted += PromptSwitchMode;
            AutomaticMapper.onImageUploaded += OnImageUploaded;
            AutomaticMapper.onImageSubmitted += OnImageSubmitted;
        }

        private void OnDisable()
        {
            AutomaticMapper.onImageSubmitted -= OnImageSubmitted;
            AutomaticMapper.onImageUploaded -= OnImageUploaded;
            AutomaticMapper.onMapSubmitted -= PromptSwitchMode;
            m_ArPointCloudManager.pointCloudsChanged -= PointCloudManager_pointCloudsChanged;
        }

        private void Update()
        {
            CheckCameraMovement();

            if (m_WaitForUpload)
            {
                if (m_ImagesUploaded == m_ImagesSubmitted)
                {
                    DemoAppManager.Instance.ShowStatusText(false);
                    m_ConstructPrompt.SetActive(true);
                    m_WaitForUpload = false;
                }
            }
        }

        private void OnImageUploaded()
        {
            m_ImagesUploaded++;
        }

        private void OnImageSubmitted()
        {
            m_ImagesSubmitted++;
        }

        public void PromptSwitchMode()
        {
            m_SwitchModePrompt.SetActive(true);
        }

        public void SwitchToContentPlacementMode()
        {
            DemoAppManager.Instance.SetStateContentPlacement();
        }

        public void SwitchToMainMenu()
        {
            DemoAppManager.Instance.SetStateMainMenu();
        }

        private void CheckCameraMovement()
        {
            Vector3 camPos = m_MainCamera.transform.position;
            Quaternion camRot = m_MainCamera.transform.rotation;

            float deltaPosMagnitude = (camPos - camPrevPos).magnitude;
            float deltaRotAngle = Quaternion.Angle(camRot, camPrevRot);

            float posThreshold = 0.02f;
            float angleThreshold = 25f;

            //if (deltaPosMagnitude > posThreshold || deltaRotAngle > angleThreshold)
            if (deltaPosMagnitude > posThreshold)
            {
                m_CameraHasMoved = true;
            }
            else
            {
                m_CameraHasMoved = false;
            }
        }

        private void PointCloudManager_pointCloudsChanged(ARPointCloudChangedEventArgs obj)
        {
            m_CurrentPoints.Clear();

            List<ARPoint> addedPoints = new List<ARPoint>();
            foreach (var pointCloud in obj.added)
            {
                foreach (var pos in pointCloud.positions)
                {
                    ARPoint newPoint = new ARPoint(pos);
                    addedPoints.Add(newPoint);
                }
            }
            List<ARPoint> updatedPoints = new List<ARPoint>();
            foreach (var pointCloud in obj.updated)
            {
                foreach (var pos in pointCloud.positions)
                {
                    ARPoint newPoint = new ARPoint(pos);
                    updatedPoints.Add(newPoint);
                    m_CurrentPoints.Add(newPoint);
                }
            }
        }

        private void InitMesh()
        {
            m_PointCloud = new GameObject("Mapper Point Cloud Visualizer", typeof(MeshFilter), typeof(MeshRenderer));
            
            m_MeshFilter = m_PointCloud.GetComponent<MeshFilter>();
            m_MeshRenderer = m_PointCloud.GetComponent<MeshRenderer>();
            m_Mesh = new Mesh();
            m_MeshFilter.mesh = m_Mesh;

            Material material = new Material(Shader.Find("Immersal/Point Cloud"));
            m_MeshRenderer.material = material;
            m_MeshRenderer.material.SetFloat("_PointSize", m_PointSize);
            m_MeshRenderer.material.SetFloat("_PerspectiveEnabled", 1f);
            m_MeshRenderer.material.SetColor("_PointColor", m_PointColor);
        }

        public void CreateCloud(Vector3[] points, int totalPoints, Matrix4x4 offset)
        {
            int numPoints = totalPoints >= MAX_VERTICES ? MAX_VERTICES : totalPoints;
            int[] indices = new int[numPoints];
            Vector3[] pts = new Vector3[numPoints];
            Color32[] col = new Color32[numPoints];
            for (int i = 0; i < numPoints; ++i)
            {
                indices[i] = i;
                pts[i] = offset.MultiplyPoint3x4(points[i]);
                col[i] = m_PointColor;
            }

            m_Mesh.Clear();
            m_Mesh.vertices = pts;
            m_Mesh.colors32 = col;
            m_Mesh.SetIndices(indices, MeshTopology.Points, 0);
            m_Mesh.bounds = new Bounds(transform.position, new Vector3(float.MaxValue, float.MaxValue, float.MaxValue));
        }

        public void CreateCloud(Vector3[] points, int totalPoints)
        {
            CreateCloud(points, totalPoints, Matrix4x4.identity);
        }

        private void UpdatePoints()
        {
            foreach (ARPoint p in m_CurrentPoints)
            {
                m_Points.Add(p);
            }

            int maxPoints = m_Points.Count >= MAX_VERTICES ? MAX_VERTICES : m_Points.Count;
            Vector3[] points = new Vector3[maxPoints];

            for (int i = 0; i < maxPoints; i++)
            {
                float x = m_Points[i].x;
                float y = m_Points[i].y;
                float z = m_Points[i].z;

                points[i] = new Vector3(x, y, z);
            }

            CreateCloud(points, points.Length);
        }

        private void ResetPoints()
        {
            m_Points.Clear();
            Vector3[] points = new Vector3[0];

            CreateCloud(points, points.Length);
        }

        public void ToggleMapping()
        {
            if (!m_IsMapping)
            {
                StartMapping();
            }
            else
            {
                StopMapping(true);
            }
        }

        public void CancelMapping()
        {
            StopMapping(false);
        }

        private void StartMapping()
        {
            m_AutomaticMapper.ResetMapperPictures(true);
            ResetPoints();

            m_IsMapping = true;
            m_ForegroundImage.fillAmount = 0.01f;
            m_CaptureButtonIcon.sprite = m_CancelCaptureSprite;
            m_CaptureButtonText.text = "Cancel";

            StartCoroutine("CaptureImages");
            m_ProgressBarCanvasGroup.GetComponent<Fader>().fadeTime = 0.5f;
            m_ProgressBarCanvasGroup.GetComponent<Fader>().FadeIn();

            Debug.Log("Auto mapping started");
        }

        private void StopMapping(bool submitMap)
        {
            ResetPoints();

            m_IsMapping = false;
            m_CaptureButtonIcon.sprite = m_StartCaptureSprite;
            m_CaptureButtonText.text = "Start Capture";

            StopCoroutine("CaptureImages");
            m_ProgressBarCanvasGroup.GetComponent<Fader>().fadeTime = 0.15f;
            m_ProgressBarCanvasGroup.GetComponent<Fader>().FadeOut();

            Debug.Log("Auto mapping stopped");

            if(m_ImagesSubmitted >= m_MinImages && submitMap)
            {
                if (m_ImagesSubmitted == m_ImagesUploaded)
                {
                    m_ConstructPrompt.SetActive(true);
                }
                else
                {
                    m_WaitForUpload = true;
                    DemoAppManager.Instance.ShowStatusText(true, "Please wait while uploading images...");
                }
            }
            else if(m_ImagesSubmitted < m_MinImages && submitMap)
            {
                NotificationManager.Instance.GenerateWarning("Not enough captured images!");
            }
            else
            {
                // Cancelled capture
            }
        }

        public void SubmitForConstruction()
        {
            string mapName = "null";
            if (m_ConstructPromptMapNameInputField != null)
            {
                mapName = m_ConstructPromptMapNameInputField.text;
            }

            m_AutomaticMapper.Construct(mapName, m_PreservePoses, m_PublicMapToggle.isOn);
            Debug.Log("Map submitted for construction");
            //NotificationManager.Instance.GenerateSuccess("Map submitted for construction");
        }

        private IEnumerator CaptureImages()
        {
            m_ImagesSubmitted = 0;
            m_ImagesUploaded = 0;
            float t = 0f;
            float promptThreshold = 3f;
            bool promptActive = false;

            while (m_ImagesSubmitted < m_MaxImages)
            {
                t += Time.deltaTime;

                int nbDots = Mathf.FloorToInt((Time.time * 2f) % 3f) + 1;
                string dots = new string('.', nbDots);
                m_ProgressBarText.text = "Capturing" + dots;

                if(m_ImagesSubmitted > m_MinImages)
                {
                    m_CaptureButtonIcon.sprite = m_StopCaptureSprite;
                    m_CaptureButtonText.text = "Stop Capture";
                }

                if (t > m_ImageInterval && m_CameraHasMoved)
                {
                    m_MoveDevicePromptCanvasGroup.GetComponent<Fader>().fadeTime = 0.15f;
                    m_MoveDevicePromptCanvasGroup.GetComponent<Fader>().FadeOut();
                    promptActive = false;

                    float progressStart = (float)m_ImagesSubmitted / (float)m_MaxImages;
                    float progressEnd = progressStart + 1f / (float)m_MaxImages;
                    StartCoroutine(IncrementProgressBar(progressStart, progressEnd));

                    // Capture image
                    m_AutomaticMapper.Capture();
                    t = 0f;
                    camPrevPos = m_MainCamera.transform.position;
                    camPrevRot = m_MainCamera.transform.rotation;

                    UpdatePoints();
                }
                else if (t > promptThreshold && !promptActive)
                {
                    // Display Movement Prompt
                    Debug.Log("Please move the device");
                    m_MoveDevicePromptCanvasGroup.GetComponent<Fader>().fadeTime = 1f;
                    m_MoveDevicePromptCanvasGroup.GetComponent<Fader>().FadeIn();
                    promptActive = true;
                }

                yield return null;
            }
            StopMapping(true);
        }

        private IEnumerator IncrementProgressBar(float start, float end)
        {
            float blend = 0;
            float length = 0.7f;

            while (blend <= 1f)
            {
                blend = (blend + Time.deltaTime) / length;
                float value = Mathf.Lerp(start, end, Mathf.Clamp(blend, 0.01f, 1f));
                m_ForegroundImage.fillAmount = value;

                yield return null;
            }
        }
    }
}