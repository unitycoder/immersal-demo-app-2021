using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Parse.LiveQuery;
using Parse;

namespace Immersal.Samples.DemoApp.ARO
{
    public class AROManager : MonoBehaviour
    {
		private static AROManager instance = null;

        [SerializeField]
        private List<GameObject> prefabs;
        [SerializeField]
        private GameObject defaultPrefab;
        [SerializeField]
        private Transform goContainer;
        [SerializeField]
        private GameObject m_UICanvas;
        [SerializeField]
        private AROPlacer AROPlacer;
        private ParseClient m_ParseClient;
        private ParseLiveQueryClient m_ParseLiveClient;
        private RealtimeQuery<ParseObject> m_RealtimeQuery;
        private Dictionary<string, GameObject> m_GOs;
        private Dictionary<string, ParseObject> m_AROs;

        private bool isInitialized = false;

        public ParseObject currentScene { get; set; }
        public Transform GOContainer { get => goContainer; set => goContainer = value; }
        public bool IsInitialized { get => isInitialized; }
        /// <summary>
        /// This is just a little bool that tracks whether we are subcribed, it is a bit of a hack to show the emuilation of entry for new subcribers to the realtimequyery
        /// </summary>
        public bool m_IsSubscribed;

        public static AROManager Instance
        {
            get
            {
#if UNITY_EDITOR
                if (instance == null && !Application.isPlaying)
                {
                    instance = UnityEngine.Object.FindObjectOfType<AROManager>();
                }
#endif
                if (instance == null)
                {
                    Debug.LogError("No AROManager instance found. Ensure one exists in the scene.");
                }
                return instance;
            }
        }

        void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
            if (instance != this)
            {
                Debug.LogError("There must be only one AROManager object in a scene.");
                UnityEngine.Object.DestroyImmediate(this);
                return;
            }
        }

        public void Reset()
        {
            isInitialized = false;
            DeleteAllGOs();
            m_GOs = null;
            m_AROs = null;

            if (m_RealtimeQuery != null)
                m_RealtimeQuery.Destroy();

            if (AROPlacer != null)
                AROPlacer.PlacementCompleted -= AROPlaced;
        }

        void Start()
        {
            m_ParseClient = ParseManager.Instance.parseClient;
            m_ParseLiveClient = ParseManager.Instance.parseLiveClient;
        }

        public void Initialize()
        {
            m_GOs = new Dictionary<string, GameObject>();
            m_AROs = new Dictionary<string, ParseObject>();

            if (AROPlacer != null)
                AROPlacer.PlacementCompleted += AROPlaced;

            isInitialized = true;
        }

        private void OnDestroy()
        {
            if (m_RealtimeQuery != null)
                m_RealtimeQuery.Destroy();

            if (m_RealtimeQuery != null)
                m_RealtimeQuery.Destroy();

            if (AROPlacer != null)
                AROPlacer.PlacementCompleted -= AROPlaced;
        }

        public void Unsubscribe()
        {
            if (m_IsSubscribed)
            {
                foreach (GameObject go in m_GOs.Values)
                {
                    GameObject.Destroy(go);
                }
                m_GOs = new Dictionary<string, GameObject>();
                m_AROs = new Dictionary<string, ParseObject>();

                m_RealtimeQuery.OnCreate -= ARO_OnCreate;
                m_RealtimeQuery.OnDelete -= ARO_OnDelete;
                m_RealtimeQuery.OnEnter -= ARO_OnEnter;
                m_RealtimeQuery.OnLeave -= ARO_OnLeave;
                m_RealtimeQuery.OnUpdate -= ARO_OnUpdate;
                m_IsSubscribed = false;
            }
        }

        public void Subscribe()
        {
            if (!m_IsSubscribed)
            {
                m_RealtimeQuery.OnCreate += ARO_OnCreate;
                m_RealtimeQuery.OnDelete += ARO_OnDelete;
                m_RealtimeQuery.OnEnter += ARO_OnEnter;
                m_RealtimeQuery.OnLeave += ARO_OnLeave;
                m_RealtimeQuery.OnUpdate += ARO_OnUpdate;
                m_IsSubscribed = true;
            }
        }

        public async Task<ParseObject> AddScene(int mapId)
        {
            ParseObject scene = new ParseObject("Scene");
            scene["mapId"] = mapId;
            scene["aros"] = new List<string>();
            await scene.SaveAsync();
            return scene;
        }

        public async Task<ParseObject> GetSceneByMapId(int mapId)
        {
            ParseQuery<ParseObject> query = m_ParseClient.GetQuery("Scene").WhereEqualTo("mapId", mapId);
            ParseObject scene = await query.FirstOrDefaultAsync();
            return scene;
        }

        public async Task<ParseObject> AddARO(IDictionary<string, object> data, int prefabIndex)
        {
            if (currentScene == null)
                return null;
            
            ParseObject aro = new ParseObject("ARO");
            aro["sceneId"] = currentScene.ObjectId;
            aro["author"] = m_ParseClient.GetCurrentUser().Username;
            aro["prefabIndex"] = prefabIndex;
            aro["data"] = data;

            Transform cameraTransform = Camera.main.transform;
            Vector3 localPos = goContainer.InverseTransformPoint(cameraTransform.position + cameraTransform.forward);
            Quaternion localRot = Quaternion.Inverse(goContainer.rotation) * Quaternion.identity;

            aro["position_x"] = localPos.x;
            aro["position_y"] = localPos.y;
            aro["position_z"] = localPos.z;
            aro["rotation_x"] = localRot.x;
            aro["rotation_y"] = localRot.y;
            aro["rotation_z"] = localRot.z;
            aro["rotation_w"] = localRot.w;
            await aro.SaveAsync();

            currentScene.AddUniqueToList("aros", aro.ObjectId);
            await currentScene.SaveAsync();

            //PlaceAROWithPlacer(aro.ObjectId);

            return aro;
        }

        public async void UpdateAROData(string id, IDictionary<string, object> newData)
        {
            ParseObject aro = m_AROs[id];
            aro["data"] = newData;
            await aro.SaveAsync();
        }

        public async void DeleteARO(string id)
        {
            ParseObject aro = m_AROs[id];
            await aro.DeleteAsync();

            currentScene.RemoveAllFromList("aros", new List<string> { id });
            await currentScene.SaveAsync();
        }

        public async void DeleteAllAROs()
        {
            if (currentScene == null)
            {
                Debug.Log("Unable to delete (no map loaded?)");
                return;
            }
            currentScene.RemoveAllFromList("aros", m_AROs.Values);
            await currentScene.SaveAsync();
            await m_ParseClient.DeleteObjectsAsync(m_AROs.Values);
        }

        public async void MoveARO(string id, Vector3 position)
        {
            ParseObject aro = m_AROs[id];
            aro["position_x"] = position.x;
            aro["position_y"] = position.y;
            aro["position_z"] = position.z;
            await aro.SaveAsync();
        }

        public void PlaceAROWithPlacer(string uid)
        {
            m_GOs[uid]?.SetActive(false);
            m_UICanvas?.SetActive(false);

            if (AROPlacer?.CurrentState == AROPlacer.AROPlacerState.Off)
            {
                int ghostIndex = 0;
                ParseObject aro = m_AROs[uid];
                ghostIndex = aro.Get<int>("prefabIndex");

                AROPlacer.StartPlacing(uid, ghostIndex);
            }
        }
        
        public void PlaceARO(string uid, Pose worldPose)
        {
            AROPlaced(uid, worldPose);
        }

        public async void UpdateAROPose(string uid, Pose newPose)
        {
            ParseObject aro = m_AROs[uid];

            aro["position_x"] = newPose.position.x;
            aro["position_y"] = newPose.position.y;
            aro["position_z"] = newPose.position.z;
            aro["rotation_x"] = newPose.rotation.x;
            aro["rotation_y"] = newPose.rotation.y;
            aro["rotation_z"] = newPose.rotation.z;
            aro["rotation_w"] = newPose.rotation.w;
            await aro.SaveAsync();
        }

        /// <summary>
        /// Creates a realtime query and listens to the events
        /// </summary>
        public void StartRealtimeQuery()
        {
            Reset();
            Initialize();

            ParseQuery<ParseObject> query = m_ParseClient.GetQuery("ARO").WhereEqualTo("sceneId", currentScene.ObjectId);

            m_GOs = new Dictionary<string, GameObject>();
            m_AROs = new Dictionary<string, ParseObject>();

            m_RealtimeQuery = new RealtimeQuery<ParseObject>(query, slowAndSafe: true);
            m_RealtimeQuery.OnCreate += ARO_OnCreate;
            m_RealtimeQuery.OnDelete += ARO_OnDelete;
            m_RealtimeQuery.OnEnter += ARO_OnEnter;
            m_RealtimeQuery.OnLeave += ARO_OnLeave;
            m_RealtimeQuery.OnUpdate += ARO_OnUpdate;
            m_IsSubscribed = true;
        }

        /// <summary>
        /// Something about one of our objects has been changed
        /// </summary>
        /// <param name="obj">the changed object</param>
        private void ARO_OnUpdate(ParseObject obj)
        {
            UpdateGO(obj);
        }

        /// <summary>
        /// We have had an object leave our query
        /// </summary>
        /// <param name="obj">the object that is leaving</param>
        private void ARO_OnLeave(ParseObject obj)
        {
            if (m_AROs.ContainsKey(obj.ObjectId))
                m_AROs.Remove(obj.ObjectId);
            
            DeleteGO(obj);
        }

        /// <summary>
        /// We have had a new object enter our query
        /// </summary>
        /// <param name="obj">the object that entered the query</param>
        private void ARO_OnEnter(ParseObject obj)
        {
            if (!m_AROs.ContainsKey(obj.ObjectId))
                m_AROs.Add(obj.ObjectId, obj);
            
            CreateGO(obj);
        }

        /// <summary>
        /// One of the objects we were looking at was deleted
        /// </summary>
        /// <param name="obj">the object that was deleted</param>
        private void ARO_OnDelete(ParseObject obj)
        {
            if (m_AROs.ContainsKey(obj.ObjectId))
                m_AROs.Remove(obj.ObjectId);
            
            DeleteGO(obj);
        }

        /// <summary>
        /// A new object has been created that matches the query
        /// </summary>
        /// <param name="obj">the object that was created</param>
        private void ARO_OnCreate(ParseObject obj)
        {
            if (!m_AROs.ContainsKey(obj.ObjectId))
                m_AROs.Add(obj.ObjectId, obj);
            
            CreateGO(obj);
        }

        /// <summary>
        /// Creates a new AR objects and updates its values as provided by the database
        /// </summary>
        /// <param name="arObject">the parse object retreived</param>
        private void CreateGO(ParseObject arObject)
        {
            AddNewGO(arObject);
            UpdateGO(arObject);
        }

        /// <summary>
        /// Delete the AR objects
        /// </summary>
        /// <param name="arObject">the object we want to delete</param>
        private void DeleteGO(ParseObject arObject)
        {
            if (m_GOs.ContainsKey(arObject.ObjectId))
            {
                GameObject.Destroy(m_GOs[arObject.ObjectId]);
                m_GOs.Remove(arObject.ObjectId);
            }
        }

        /// <summary>
        /// Updates the AR object based on the values provided
        /// </summary>
        /// <param name="arObject">the updated data</param>
        private void UpdateGO(ParseObject arObject)
        {
            Vector3 pos = new Vector3(arObject.Get<float>("position_x"), arObject.Get<float>("position_y"), arObject.Get<float>("position_z"));
            Quaternion rot = new Quaternion(arObject.Get<float>("rotation_x"), arObject.Get<float>("rotation_y"), arObject.Get<float>("rotation_z"), arObject.Get<float>("rotation_w"));
            GameObject arGO = m_GOs[arObject.ObjectId];

            ARODataHandler handler = arGO.GetComponent<ARODataHandler>();
            if (handler == null)
            {
                handler = arGO.AddComponent<ARODataHandler>();
            }

            handler.Uid = arObject.ObjectId;
            IDictionary<string, object> data = arObject.Get<IDictionary<string, object>>("data");
            handler.UpdateData(data);

            arGO.transform.localPosition = pos;
            arGO.transform.localRotation = rot;
            arGO.SetActive(true);
        }

        private void AddNewGO(ParseObject aro)
        {
            if (m_GOs == null)
                return;

            if (m_GOs.ContainsKey(aro.ObjectId))
                return;

            int prefabIndex = 0;
            string url = "";
            IDictionary<string, object> data = aro.Get<IDictionary<string, object>>("data");

            switch (data["Prefab"])
            {
                case "Poster":
                    prefabIndex = 0;
                    url = data["PosterURL"] as string;
                    break;

                case "AR Diamond":
                    prefabIndex = 1;
                    break;

                default:
                    prefabIndex = 0;
                    break;
            }

            GameObject go;
            Transform cameraTransform = Camera.main.transform;

            if (prefabIndex >= 0 && prefabIndex < prefabs.Count)
            {
                Debug.LogFormat("[AROM] Instantiating prefab: {0}", prefabIndex);
                go = Instantiate(prefabs[prefabIndex], cameraTransform.position + cameraTransform.forward, Quaternion.identity, goContainer.transform);

                if (prefabIndex == 0)
                {
                    Immersal.Util.RemoteTexture rt = go.GetComponent<Immersal.Util.RemoteTexture>();
                    rt.url = url;
                }
            }
            else
            {
                Debug.Log("[AROM] Prefab index out of range. Instantiating default prefab.");
                go = Instantiate(defaultPrefab, cameraTransform.position + cameraTransform.forward, Quaternion.identity, goContainer.transform);
            }

            Debug.LogFormat("[AROM] Adding GO: {0}", aro.ObjectId);

            ARODataHandler handler = go.GetComponent<ARODataHandler>();
            if (handler == null)
            {
                handler = go.AddComponent<ARODataHandler>();
            }

            handler.Uid = aro.ObjectId;
            handler.CurrentData = data;

            if (!m_GOs.ContainsKey(aro.ObjectId))
                m_GOs.Add(aro.ObjectId, go);
        }

        public async void AddNewPoster(string url = "https://upload.wikimedia.org/wikipedia/commons/7/7b/Obverse_of_the_series_2009_%24100_Federal_Reserve_Note.jpg")
        {
            Dictionary<string, object> data = new Dictionary<string, object>();
            data["PosterURL"] = url;
            data["Prefab"] = "Poster";
            await AddARO(data, 0);
        }

        public async void AddNewDiamond()
        {
            Dictionary<string, object> data = new Dictionary<string, object>();
            data["Prefab"] = "AR Diamond";
            await AddARO(data, 1);
        }

        public async void AddNewObject()
        {
            Dictionary<string, object> data = new Dictionary<string, object>();
            data["Prefab"] = "AR Diamond";
            await AddARO(data, 1);
        }

        private void AROPlaced(string uid, Pose worldPose)
        {
            Vector3 localPos = goContainer.InverseTransformPoint(worldPose.position);
            Quaternion localRot = Quaternion.Inverse(goContainer.rotation) * worldPose.rotation;

            UpdateAROPose(uid, new Pose(localPos, localRot));
            
            m_UICanvas?.SetActive(true);

            ARODataHandler handler = m_GOs[uid]?.GetComponent<ARODataHandler>();
            if (handler == null)
            {
                Debug.Log("[AROM] Cant find ARO data handler on GO. Adding new.");
                handler = m_GOs[uid]?.AddComponent<ARODataHandler>();
            }
            handler.AROMoved();
        }

        public void DeleteAllGOs()
        {
            if (m_GOs != null)
            {
                foreach (KeyValuePair<string, GameObject> kvp in m_GOs)
                {
                    if (kvp.Value != null)
                        Destroy(kvp.Value);
                }
                m_GOs.Clear();
            }
        }
    }
}
