using System.IO;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AssetIconGenerator.Editor
{
    public class AssetIconGeneratorWindow : OdinEditorWindow
    {
        private const string TempCameraName = "Temp_Icon_Camera";
        private const string TempLightName = "Temp_Icon_Light";

        private Scene _previewScene;
        private bool _didScavengeLegacy;

        [MenuItem("Tools/Asset Icon Generator 📸")]
        private static void OpenWindow()
        {
            var window = GetWindow<AssetIconGeneratorWindow>("Icon Generator");
            window.minSize = new Vector2(820, 560);
            window.Show();
        }

        // --- LEFT COLUMN: SETTINGS ---

        [HorizontalGroup("MainLayout", 0.5f, PaddingRight = 10f)]
        [VerticalGroup("MainLayout/Settings")]
        [Title("Target Settings", "Prefab or 3D Model to render", TitleAlignments.Left)]
        [Required("Please assign a prefab or model asset")]
        [HideLabel, PreviewField(100, ObjectFieldAlignment.Left)]
        [OnValueChanged(nameof(OnTargetAssetChanged))]
        public GameObject TargetAsset;

        [VerticalGroup("MainLayout/Settings")]
        [BoxGroup("MainLayout/Settings/Transform Settings", ShowLabel = true)]
        [LabelText("Object Position")]
        [Tooltip("Slides the asset inside the frame without changing camera angle. Negative Y moves it down on screen.")]
        [OnValueChanged(nameof(OnSettingsChanged))]
        public Vector3 ObjectPosition = Vector3.zero;

        [VerticalGroup("MainLayout/Settings")]
        [BoxGroup("MainLayout/Settings/Transform Settings")]
        [LabelText("Object Rotation")]
        [OnValueChanged(nameof(OnSettingsChanged))]
        public Vector3 ObjectRotation = new Vector3(0f, 45f, 0f);

        [VerticalGroup("MainLayout/Settings")]
        [BoxGroup("MainLayout/Settings/Camera Settings", ShowLabel = true)]
        [LabelText("Camera Offset")]
        [OnValueChanged(nameof(OnSettingsChanged))]
        public Vector3 CameraOffset = new Vector3(0f, 1f, -5f);

        [VerticalGroup("MainLayout/Settings")]
        [BoxGroup("MainLayout/Settings/Camera Settings")]
        [LabelText("Field of View (FOV)")]
        [Range(10f, 120f)]
        [OnValueChanged(nameof(OnSettingsChanged))]
        public float FieldOfView = 60f;

        [VerticalGroup("MainLayout/Settings")]
        [BoxGroup("MainLayout/Settings/Camera Settings")]
        [Button("Frame Target Asset 🎯", ButtonSizes.Small)]
        private void FrameTargetAsset()
        {
            if (TargetAsset == null) return;

            EnsurePreviewScene();
            ClearPreviewSceneRoots();

            GameObject tempInstance = null;
            try
            {
                // Frame against rotation only — Object Position is a post-frame composition slide.
                tempInstance = SpawnTargetInPreviewScene();
                tempInstance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(ObjectRotation));

                var renderers = tempInstance.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    Bounds bounds = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++)
                    {
                        bounds.Encapsulate(renderers[i].bounds);
                    }

                    float maxDimension = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                    float distance = maxDimension / (2f * Mathf.Tan(0.5f * FieldOfView * Mathf.Deg2Rad));

                    CameraOffset = bounds.center + new Vector3(0f, 0f, -distance * 1.2f);
                }
            }
            finally
            {
                DestroyImmediateSafe(tempInstance);
            }

            OnSettingsChanged();
        }

        [VerticalGroup("MainLayout/Settings")]
        [BoxGroup("MainLayout/Settings/Lighting & Background", ShowLabel = true)]
        [LabelText("Spawn Temp Light")]
        [OnValueChanged(nameof(OnSettingsChanged))]
        public bool SpawnTempLight = true;

        [VerticalGroup("MainLayout/Settings")]
        [BoxGroup("MainLayout/Settings/Lighting & Background")]
        [LabelText("Transparent Background")]
        [OnValueChanged(nameof(OnSettingsChanged))]
        public bool TransparentBackground = true;

        [VerticalGroup("MainLayout/Settings")]
        [BoxGroup("MainLayout/Settings/Lighting & Background")]
        [HideIf(nameof(TransparentBackground))]
        [LabelText("Background Color")]
        [OnValueChanged(nameof(OnSettingsChanged))]
        public Color BackgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);

        [VerticalGroup("MainLayout/Settings")]
        [BoxGroup("MainLayout/Settings/Export Settings", ShowLabel = true)]
        [LabelText("Resolution")]
        [OnValueChanged(nameof(OnSettingsChanged))]
        public Vector2Int Resolution = new Vector2Int(1024, 1024);

        [VerticalGroup("MainLayout/Settings")]
        [BoxGroup("MainLayout/Settings/Export Settings")]
        [FolderPath(AbsolutePath = false)]
        [LabelText("Save Directory")]
        public string SaveDirectory = "Assets";

        [VerticalGroup("MainLayout/Settings")]
        [BoxGroup("MainLayout/Settings/Export Settings")]
        [LabelText("File Name")]
        public string FileName = "Icon_New";


        // --- RIGHT COLUMN: PREVIEW & EXPORT ---

        [VerticalGroup("MainLayout/Preview")]
        [Title("Preview", "Real-time render output", TitleAlignments.Centered)]
        [HideLabel]
        [ShowInInspector]
        [PreviewField(350, ObjectFieldAlignment.Center)]
        private Texture2D _previewTexture;

        [VerticalGroup("MainLayout/Preview")]
        [LabelText("Auto-Update Preview")]
        [Tooltip("Automatically refreshes preview when settings change")]
        public bool AutoUpdatePreview = true;

        [VerticalGroup("MainLayout/Preview")]
        [Button("Refresh Preview 🔄", ButtonSizes.Medium)]
        [GUIColor(0.8f, 0.8f, 0.8f)]
        private void UpdatePreview()
        {
            if (TargetAsset == null)
            {
                ClearPreview();
                return;
            }

            const float maxPreviewSize = 512f;
            float aspect = (float)Resolution.x / Resolution.y;

            int previewWidth = Resolution.x > Resolution.y ? (int)maxPreviewSize : (int)(maxPreviewSize * aspect);
            int previewHeight = Resolution.y > Resolution.x ? (int)maxPreviewSize : (int)(maxPreviewSize / aspect);

            ClearPreview();
            _previewTexture = RenderImage(previewWidth, previewHeight);
        }

        [VerticalGroup("MainLayout/Preview")]
        [Button("Capture Icon 📸", ButtonSizes.Gigantic)]
        [GUIColor(0.2f, 0.8f, 0.3f)]
        private void GenerateScreenshot()
        {
            if (TargetAsset == null)
            {
                Debug.LogError("[IconGenerator] Please assign a Target Asset first!");
                return;
            }

            Texture2D finalScreenshot = RenderImage(Resolution.x, Resolution.y);
            if (finalScreenshot == null)
            {
                Debug.LogError("[IconGenerator] Failed to render icon.");
                return;
            }

            byte[] bytes = finalScreenshot.EncodeToPNG();
            DestroyImmediate(finalScreenshot);

            string relativeFolderPath = SaveDirectory.Replace("\\", "/");
            if (!relativeFolderPath.StartsWith("Assets"))
            {
                relativeFolderPath = "Assets";
            }

            string fullDirectoryPath = Path.Combine(Directory.GetCurrentDirectory(), relativeFolderPath);
            if (!Directory.Exists(fullDirectoryPath))
            {
                Directory.CreateDirectory(fullDirectoryPath);
            }

            string cleanFileName = string.IsNullOrWhiteSpace(FileName) ? TargetAsset.name : FileName;
            string filePath = Path.Combine(relativeFolderPath, $"{cleanFileName}.png").Replace("\\", "/");

            File.WriteAllBytes(filePath, bytes);
            AssetDatabase.Refresh();

            Object savedAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(filePath);
            if (savedAsset != null)
            {
                EditorGUIUtility.PingObject(savedAsset);
            }

            Debug.Log($"<color=green>[IconGenerator] Icon successfully saved to: {filePath}</color>");
        }

        private void OnTargetAssetChanged()
        {
            if (TargetAsset != null)
            {
                FileName = TargetAsset.name;
            }
            OnSettingsChanged();
        }

        private void OnSettingsChanged()
        {
            if (AutoUpdatePreview)
            {
                UpdatePreview();
            }
        }

        // --- LIFECYCLE ---

        protected override void OnEnable()
        {
            base.OnEnable();
            if (!_didScavengeLegacy)
            {
                _didScavengeLegacy = true;
                ScavengeLegacyTempObjects();
            }
        }

        protected override void OnDisable()
        {
            DisposePreviewScene();
            ClearPreview();
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            DisposePreviewScene();
            ClearPreview();
            base.OnDestroy();
        }

        // --- RENDER LOGIC ---

        private Texture2D RenderImage(int width, int height)
        {
            EnsurePreviewScene();
            ClearPreviewSceneRoots();

            // Object Position only slides the mesh in the frame. Camera always looks at the
            // preview origin so changing Y/X/Z never tilts the view (no fake "rotation").
            GameObject instance = null;
            GameObject cameraObj = null;
            GameObject lightObj = null;
            RenderTexture rt = null;
            Texture2D screenShot = null;

            try
            {
                instance = SpawnTargetInPreviewScene();
                instance.transform.SetPositionAndRotation(
                    ObjectPosition,
                    Quaternion.Euler(ObjectRotation));

                cameraObj = CreateGameObjectInPreviewScene(TempCameraName);
                cameraObj.transform.position = CameraOffset;
                cameraObj.transform.LookAt(Vector3.zero);

                Camera cam = cameraObj.AddComponent<Camera>();
                cam.scene = _previewScene;
                cam.fieldOfView = FieldOfView;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = TransparentBackground ? new Color(0f, 0f, 0f, 0f) : BackgroundColor;
                cam.enabled = false;

                if (SpawnTempLight)
                {
                    lightObj = CreateGameObjectInPreviewScene(TempLightName);
                    Light light = lightObj.AddComponent<Light>();
                    light.type = LightType.Directional;
                    light.intensity = 1.2f;
                    lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                }

                rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;

                screenShot = new Texture2D(width, height, TextureFormat.ARGB32, false);

                cam.Render();

                RenderTexture.active = rt;
                screenShot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                screenShot.Apply();

                return screenShot;
            }
            catch
            {
                DestroyImmediateSafe(screenShot);
                throw;
            }
            finally
            {
                if (cameraObj != null)
                {
                    Camera cam = cameraObj.GetComponent<Camera>();
                    if (cam != null)
                    {
                        cam.targetTexture = null;
                    }
                }

                RenderTexture.active = null;
                DestroyImmediateSafe(rt);
                DestroyImmediateSafe(cameraObj);
                DestroyImmediateSafe(instance);
                DestroyImmediateSafe(lightObj);
            }
        }

        private void ClearPreview()
        {
            if (_previewTexture != null)
            {
                DestroyImmediate(_previewTexture);
                _previewTexture = null;
            }
        }

        private void EnsurePreviewScene()
        {
            if (_previewScene.IsValid())
            {
                return;
            }

            _previewScene = EditorSceneManager.NewPreviewScene();
        }

        private void DisposePreviewScene()
        {
            if (!_previewScene.IsValid())
            {
                return;
            }

            EditorSceneManager.ClosePreviewScene(_previewScene);
            _previewScene = default;
        }

        private void ClearPreviewSceneRoots()
        {
            if (!_previewScene.IsValid())
            {
                return;
            }

            GameObject[] roots = _previewScene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                DestroyImmediateSafe(roots[i]);
            }
        }

        private GameObject SpawnTargetInPreviewScene()
        {
            PrefabAssetType prefabType = PrefabUtility.GetPrefabAssetType(TargetAsset);
            if (prefabType != PrefabAssetType.NotAPrefab)
            {
                GameObject prefabInstance = PrefabUtility.InstantiatePrefab(TargetAsset, _previewScene) as GameObject;
                if (prefabInstance != null)
                {
                    return prefabInstance;
                }
            }

            GameObject instance = Instantiate(TargetAsset);
            EditorSceneManager.MoveGameObjectToScene(instance, _previewScene);
            return instance;
        }

        private GameObject CreateGameObjectInPreviewScene(string objectName)
        {
            GameObject go = new GameObject(objectName);
            EditorSceneManager.MoveGameObjectToScene(go, _previewScene);
            return go;
        }

        private static void DestroyImmediateSafe(Object obj)
        {
            if (obj != null)
            {
                DestroyImmediate(obj);
            }
        }

        /// <summary>
        /// Removes leftover camera/light objects left in open game scenes by older package versions.
        /// </summary>
        private static void ScavengeLegacyTempObjects()
        {
            GameObject[] all = Resources.FindObjectsOfTypeAll<GameObject>();
            int removed = 0;

            for (int i = 0; i < all.Length; i++)
            {
                GameObject go = all[i];
                if (go == null)
                {
                    continue;
                }

                if (go.name != TempCameraName && go.name != TempLightName)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(go)))
                {
                    continue;
                }

                if (EditorSceneManager.IsPreviewSceneObject(go))
                {
                    continue;
                }

                DestroyImmediate(go);
                removed++;
            }

            if (removed > 0)
            {
                Debug.Log($"[IconGenerator] Removed {removed} leftover Temp_Icon_* object(s) from open scenes.");
            }
        }
    }
}
