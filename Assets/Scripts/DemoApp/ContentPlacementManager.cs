/*===============================================================================
Copyright (C) 2020 Immersal Ltd. All Rights Reserved.

This file is part of the Immersal SDK.

The Immersal SDK cannot be copied, distributed, or made available to
third-parties for commercial purposes without written permission of Immersal Ltd.

Contact sdk@immersal.com for licensing requests.
===============================================================================*/

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using Immersal.AR;
using Immersal.REST;
using Immersal.Samples.DemoApp.ARO;
using Immersal.Samples.Mapping;
using Parse;

namespace Immersal.Samples.DemoApp
{
    [RequireComponent(typeof(AROManager))]
    public class ContentPlacementManager : MonoBehaviour
    {
        public static bool isEditingARO = false;

        [SerializeField]
        private ARPlaneManager m_ARPlaneManager;
        [SerializeField]
        private DemoAppMapListController m_MapListController;
        [SerializeField]
        private Toggle m_AutoLocalizeToggle;
        [SerializeField]
        private Gazer m_Gazer = null;
        [SerializeField]
        private ARMap m_ARMap = null;
        [SerializeField]
        private GameObject m_PlanePrefab = null;

        private float m_TouchTime = 0f;
        private bool m_IsTouching = false;
        private Pose m_HitPose;
        private AROManager m_AROManager;

        public static ContentPlacementManager Instance
        {
            get
            {
#if UNITY_EDITOR
                if (instance == null && !Application.isPlaying)
                {
                    instance = UnityEngine.Object.FindObjectOfType<ContentPlacementManager>();
                }
#endif
                if (instance == null)
                {
                    Debug.LogError("No ContentPlacementManager instance found. Ensure one exists in the scene.");
                }
                return instance;
            }
        }

        private static ContentPlacementManager instance = null;

        public bool autoLocalize
        {
            get { return m_AutoLocalizeToggle.isOn; }
        }

        void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
            if (instance != this)
            {
                Debug.LogError("There must be only one ContentPlacementManager object in a scene.");
                UnityEngine.Object.DestroyImmediate(this);
                return;
            }
        }

        void OnEnable()
        {
            m_MapListController.MapListLoaded += OnMapListLoaded;
        }

        void OnDisable()
        {
            m_ARMap.FreeMap();
            m_AROManager.Reset();
            m_MapListController.MapListLoaded -= OnMapListLoaded;
        }

        private void OnMapListLoaded()
        {
            if (autoLocalize)
            {
                // TODO: rework on-server loc
            }
        }

        void Start()
        {
            m_AROManager = GetComponent<AROManager>();

            EnablePlaneManager(false);

            m_MapListController.dropdown.interactable = !autoLocalize;
        }

        private async void MapLocalized(int serverMapId)
        {
            List<SDKJob> maps = m_MapListController.maps;
            int index = -1;
            for (int i = 0; i < maps.Count; i++)
            {
                SDKJob map = maps[i];
                if (map.id == serverMapId)
                {
                    index = i;
                    break;
                }
            }

            if (m_MapListController.dropdown.options.Count > maps.Count)
                index++;

            if (index > 0)
            {
                m_MapListController.dropdown.SetValueWithoutNotify(index);
                NotificationManager.Instance.GenerateSuccess("Map localized successfully.");
            }

            ParseObject currentScene = await m_AROManager.GetSceneByMapId(serverMapId);
            if (currentScene == null)
            {
                currentScene = await m_AROManager.AddScene(serverMapId);
            }
            Debug.Log("currentScene: " + currentScene.ObjectId);

            m_AROManager.currentScene = currentScene;
            m_AROManager.StartRealtimeQuery();
        }

        public void SwitchToMainMenu()
        {
            DemoAppManager.Instance.SetStateMainMenu();
        }

        /*void Update()
        {
            if (isEditingARO)
            {
                m_IsTouching = false;
                m_TouchTime = 0f;
                return;
            }

            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                int id = touch.fingerId;

                if (touch.phase == TouchPhase.Began && !m_IsTouching)
                {
                    if (!EventSystem.current.IsPointerOverGameObject(id))
                    {
                        m_TouchTime = Time.time;
                        m_IsTouching = true;
                    }
                }

                if (m_IsTouching && touch.phase == TouchPhase.Ended)
                {
                    Pose pose = default;

                    if (m_Gazer.Gaze(out pose))
                    {
                        PlaneAlignment alignment;

                        if (m_Gazer.IsOnAPlane(out alignment))
                        {
                            if (Time.time - m_TouchTime <= 0.5f)
                            {
                                if (alignment == PlaneAlignment.Vertical)
                                {
                                    m_AROManager.AddNewPoster();
                                }
                                else
                                {
                                    m_AROManager.AddNewDiamond();
                                }
                            }
                            else
                            {
                                if (alignment == PlaneAlignment.Vertical)
                                {
                                    //m_URLPanel.GetComponent<Fader>().FadeIn();
                                    m_TouchTime = 0f;
                                }
                            }
                        }
                        else
                        {
                            m_AROManager.AddNewDiamond();
                        }
                    }
                    else{
                        Debug.Log("No pose found");
                        m_AROManager.AddNewDiamond();
                    }

                    m_IsTouching = false;
                }
            }
        }*/

        public void OnToggleChange(Toggle toggle)
        {
            m_MapListController.dropdown.interactable = !toggle.isOn;

            if (autoLocalize)
            {
                // TODO: rework on-server loc
            }
            else
            {
                // TODO: rework on-server loc
            }
        }

        private void EnablePlaneManager(bool isOn)
        {
            foreach (var plane in m_ARPlaneManager.trackables)
            {
                plane.gameObject.SetActive(isOn);
            }

            m_ARPlaneManager.planePrefab = (isOn) ? m_PlanePrefab : null;
        }
    }
}
