/*===============================================================================
Copyright (C) 2020 Immersal Ltd. All Rights Reserved.

This file is part of the Immersal SDK.

The Immersal SDK cannot be copied, distributed, or made available to
third-parties for commercial purposes without written permission of Immersal Ltd.

Contact sdk@immersal.com for licensing requests.
===============================================================================*/

using UnityEngine;
using Immersal.Samples.Mapping;
using Immersal.Samples.Util;
using TMPro;

namespace Immersal.Samples.DemoApp
{
    public class DemoAppManager : MonoBehaviour
    {
        public enum DemoAppState { Login, MainMenu, ContentPlacement, Mapper };
        public DemoAppState demoAppState { get; private set; } = DemoAppState.Login;

        [SerializeField]
        private GameObject m_LoginManager = null;
        [SerializeField]
        private GameObject m_MainMenu = null;
        [SerializeField]
        private GameObject m_ContenPlacementUI = null;
        [SerializeField]
        private GameObject m_MapperUI = null;
        [SerializeField]
        private GameObject m_StatusText = null;

        public static DemoAppManager Instance
        {
            get
            {
#if UNITY_EDITOR
                if (instance == null && !Application.isPlaying)
                {
                    instance = UnityEngine.Object.FindObjectOfType<DemoAppManager>();
                }
#endif
                if (instance == null)
                {
                    Debug.LogError("No DemoAppManager instance found. Ensure one exists in the scene.");
                }
                return instance;
            }
        }

        private static DemoAppManager instance = null;

        void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
            if (instance != this)
            {
                Debug.LogError("There must be only one DemoAppManager object in a scene.");
                UnityEngine.Object.DestroyImmediate(this);
                return;
            }
        }

        private void Start()
        {
            SetStateLogin();

            if (LoginManager.Instance != null)
            {
                LoginManager.Instance.OnLogin += OnLogin;
            }
        }

        private async void OnLogin()
        {
            ShowStatusText(true, "Please wait while connecting...");
            await ParseManager.Instance.AuthUser();
            ShowStatusText(false);
            SetStateMainMenu();
        }

        public void SetStateLogin()
        {
            SwitchState(DemoAppState.Login);
        }

        public void SetStateMainMenu()
        {
            SwitchState(DemoAppState.MainMenu);
        }

        public void SetStateContentPlacement()
        {
            SwitchState(DemoAppState.ContentPlacement);
        }

        public void SetStateMapper()
        {
            SwitchState(DemoAppState.Mapper);
        }

        public void ShowStatusText(bool on, string text = null)
        {
            if (text != null)
            {
                TextMeshProUGUI label = m_StatusText.GetComponent<TextMeshProUGUI>();
                label.text = text;
            }

            Fader fader = m_StatusText.GetComponent<Fader>();
            fader.ToggleFade(on);
        }
        
        private void SwitchState(DemoAppState state)
        {
            switch (state)
            {
                case DemoAppState.Login:
                    demoAppState = DemoAppState.Login;
                    m_LoginManager.SetActive(true);
                    m_MainMenu.SetActive(false);
                    m_ContenPlacementUI.SetActive(false);
                    m_MapperUI.SetActive(false);
                    break;
                case DemoAppState.MainMenu:
                    demoAppState = DemoAppState.MainMenu;
                    m_LoginManager.SetActive(false);
                    m_MainMenu.SetActive(true);
                    m_ContenPlacementUI.SetActive(false);
                    m_MapperUI.SetActive(false);
                    break;
                case DemoAppState.ContentPlacement:
                    demoAppState = DemoAppState.ContentPlacement;
                    m_LoginManager.SetActive(false);
                    m_MainMenu.SetActive(false);
                    m_ContenPlacementUI.SetActive(true);
                    m_MapperUI.SetActive(false);
                    break;
                case DemoAppState.Mapper:
                    demoAppState = DemoAppState.Mapper;
                    m_LoginManager.SetActive(false);
                    m_MainMenu.SetActive(false);
                    m_ContenPlacementUI.SetActive(false);
                    m_MapperUI.SetActive(true);
                    break;
            }
        }
    }
}