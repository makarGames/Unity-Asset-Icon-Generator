using System.IO;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace AssetIconGenerator.Editor
{
    public class AssetIconGeneratorWindow : OdinEditorWindow
    {
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

            GameObject tempInstance = Instantiate(TargetAsset);
            tempInstance.transform.position = Vector3.zero;
            tempInstance.transform.rotation = Quaternion.Euler(ObjectRotation);

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

            DestroyImmediate(tempInstance);
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

        // --- RENDER LOGIC ---

        private Texture2D RenderImage(int width, int height)
        {
            Vector3 isolationPosition = new Vector3(0f, -10000f, 0f);

            GameObject instance = Instantiate(TargetAsset, isolationPosition, Quaternion.Euler(ObjectRotation));

            GameObject cameraObj = new GameObject("Temp_Icon_Camera");
            cameraObj.transform.position = isolationPosition + CameraOffset;
            cameraObj.transform.LookAt(instance.transform);

            Camera cam = cameraObj.AddComponent<Camera>();
            cam.fieldOfView = FieldOfView;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = TransparentBackground ? new Color(0f, 0f, 0f, 0f) : BackgroundColor;

            GameObject lightObj = null;
            if (SpawnTempLight)
            {
                lightObj = new GameObject("Temp_Icon_Light");
                Light light = lightObj.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.2f;
                lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }

            RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;

            Texture2D screenShot = new Texture2D(width, height, TextureFormat.ARGB32, false);

            cam.Render();

            RenderTexture.active = rt;
            screenShot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            screenShot.Apply();

            cam.targetTexture = null;
            RenderTexture.active = null;
            DestroyImmediate(rt);
            DestroyImmediate(cameraObj);
            DestroyImmediate(instance);
            if (lightObj != null) DestroyImmediate(lightObj);

            return screenShot;
        }

        private void ClearPreview()
        {
            if (_previewTexture != null)
            {
                DestroyImmediate(_previewTexture);
                _previewTexture = null;
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            ClearPreview();
        }
    }
}
