using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

[System.Serializable] 
public class PrefabSettings
{
	public GameObject Go;
	public float originalY;
	public Vector2 offset;
	public float cameraSize;
	public float cameraRotate;
	public float cameraTilt;
};

public class IconGenerator : EditorWindow
{
	int buttonSize = 20;

	private RenderTexture renderTexture;
	private Camera _camera;
	private GameObject _camTilt;
	private GameObject _camRotate;
	private Texture2D bgTexture;
	// private vars for screenshot
	private Texture2D screenShot;
    private string dataName = "IconData3526789";

	// 4k = 3840 x 2160   1080p = 1920 x 1080
	private int imageWidth = 512;
	private int imageHeight = 512;
	private int previewSize = 300;

	// configure lighting options
	private float reflectionIntensity = 0.5f;
	private int reflectionBounces = 1;
	private float ambientIntensity = 0.75f;
	private int antiAliasing;
	private int shadowProjectionChange = 0;
	private string[] lightingOptions = new string[]{"Neutral", "Warm", "Cold"};
	private int lightingMode = 0;
	private string[] skyboxOptions = new string[] {"Icon_Skybox_Neutral", "Icon_Skybox_Warm", "Icon_Skybox_Cold"};
	private Material skyboxNeutral;
	private Material skyboxWarm;
	private Material skyboxCold;
	private GameObject lightPivot;
	private float lightRotation;
	
	// configure with raw, jpg, png
	private enum Format { RAW, JPG, PNG, PPM };
	private string[] imageOptions = new string[]{"PNG", "JPG", "RAW"};
	private int imageFormat = 0;
	private Format format = Format.PNG;

	// folder to write output (defaults to data path)
    private string exportpath;

	// base setting status
	private bool baseSettingsUnfolded = true;
    private bool skyboxSettingsUnfolded = false;
    private bool previewSettingsUnfolded = false;
    
    // preview
    private bool showBackground = false;

	// list of prefabs to generate icons
	private List<GameObject> unityGameObjects = new List<GameObject>();
	private List<GameObject> inSceneObjects = new List<GameObject>();
	private List<PrefabSettings> prefabs = new List<PrefabSettings>();
	private List<bool> controlToggles = new List<bool>();
	private int controlIndexOnFocus = -1;
	
    // scriptable object
    public IconScriptableObject iconData;

	// Add menu named "Tools" to the Window menu
    [MenuItem("Tools/IconGenerator")]
	static void Init()
	{	
		bool saveScene = EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
		if (saveScene)
		{
			// Get existing open window or if none, make a new one:
			IconGenerator window = (IconGenerator)EditorWindow.GetWindow(typeof(IconGenerator));
			window.Show();
		}
	}

	void OnEnable()
	{	
		Scene scene = EditorSceneManager.GetActiveScene();
		if (scene.name != "IconRenderer")
			EditorSceneManager.OpenScene("Assets/Editor/Scenes/IconRenderer.unity");
		EditorSceneManager.sceneOpening += SceneChange;
		RenderSettings.reflectionIntensity = reflectionIntensity;
		RenderSettings.reflectionBounces = reflectionBounces;
		RenderSettings.ambientIntensity = ambientIntensity;
		antiAliasing = QualitySettings.antiAliasing;
		QualitySettings.antiAliasing = 8;
		if (QualitySettings.shadowProjection == ShadowProjection.StableFit) {
			QualitySettings.shadowProjection = ShadowProjection.CloseFit;
			shadowProjectionChange = 1;
		}
		var guids = AssetDatabase.FindAssets(skyboxOptions[0] + " t:Material");
		foreach (var guid in guids) {
			var path = AssetDatabase.GUIDToAssetPath (guid);
			skyboxNeutral = AssetDatabase.LoadAssetAtPath (path, typeof(Material)) as Material;
		}
		guids = AssetDatabase.FindAssets(skyboxOptions[1] + " t:Material");
		foreach (var guid in guids) {
			var path = AssetDatabase.GUIDToAssetPath (guid);
			skyboxWarm = AssetDatabase.LoadAssetAtPath (path, typeof(Material)) as Material;
		}
		guids = AssetDatabase.FindAssets(skyboxOptions[2] + " t:Material");
		foreach (var guid in guids) {
			var path = AssetDatabase.GUIDToAssetPath (guid);
			skyboxCold = AssetDatabase.LoadAssetAtPath (path, typeof(Material)) as Material;
		}
        // guids = AssetDatabase.FindAssets("CHR_UNV_GreenBabySeaTurtle_Diff" + " t:Texture2D");
        // foreach (var guid in guids)
        // {
        //     var path = AssetDatabase.GUIDToAssetPath(guid);
        //     bgTexture = AssetDatabase.LoadAssetAtPath(path, typeof(Texture2D)) as Texture2D;
        // }
        guids = AssetDatabase.FindAssets(dataName);
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            iconData = AssetDatabase.LoadAssetAtPath(path, typeof(IconScriptableObject)) as IconScriptableObject;
        }
		lightPivot = GameObject.Find("LIGHTING");
		// check if iconData exists
		if (iconData == null){
			Debug.Log("Created icon data at " + Path.Combine(getCurrentFolder(), dataName + ".asset"));
			iconData = ScriptableObject.CreateInstance<IconScriptableObject>();
			AssetDatabase.CreateAsset(iconData, Path.Combine(getCurrentFolder(), dataName + ".asset"));
			AssetDatabase.SaveAssets();
			// check pre-defined export folder
        	exportpath = "Assets";
			iconData.exportpath = exportpath;
			iconData.imageWidth = imageWidth;
			iconData.imageHeight = imageHeight;
        	System.IO.Directory.CreateDirectory(exportpath);
		}
		else{
			exportpath = iconData.exportpath;
			imageWidth = iconData.imageWidth;
			imageHeight = iconData.imageHeight;
			lightRotation = iconData.lightRotation;
			imageFormat = iconData.imageFormat;
			lightingMode = iconData.lightingMode;
		}
		renderTexture = new RenderTexture (imageWidth, imageHeight, 32, RenderTextureFormat.ARGB32);
		CameraSetup();
		RenderNow();
	}

	void CameraSetup()
	{
		GameObject iconCam = new GameObject("IconCamera");
		Camera camera = iconCam.AddComponent<Camera>();
		camera.orthographic = true;
		camera.transform.position = new Vector3 (0, 8, -10);
		camera.transform.rotation = Quaternion.Euler(new Vector3 (0, 45, 0));
		camera.clearFlags = CameraClearFlags.SolidColor;
		camera.backgroundColor = Color.clear;
		GameObject camTilt = new GameObject("CAMERATILT");
		camTilt.transform.rotation = Quaternion.Euler(new Vector3 (0, 45, 0));
		iconCam.transform.SetParent(camTilt.transform);
		GameObject camRotate = new GameObject("CAMERAROTATE");
		camTilt.transform.SetParent(camRotate.transform);
		camRotate.gameObject.hideFlags = HideFlags.HideAndDontSave;
		camTilt.gameObject.hideFlags = HideFlags.HideAndDontSave;
		iconCam.gameObject.hideFlags = HideFlags.HideAndDontSave;
		_camera = camera;
		_camTilt = camTilt;
		_camRotate = camRotate;
	}

	void OnDestroy()
	{	
		EditorSceneManager.sceneOpening -= SceneChange;
		CleanUp();
	}

	void SceneChange(string path, OpenSceneMode mode)
    {
		IconGenerator window = (IconGenerator)EditorWindow.GetWindow(typeof(IconGenerator));
		window.Close();
    }

	void CleanUp()
	{
		if ((_camera != null) || GameObject.Find(_camera.name))
			DestroyImmediate(_camera.gameObject.transform.root.gameObject);
		for(int i=0; i<inSceneObjects.Count; i++)
		{
			if (inSceneObjects[i] != null)
				DestroyImmediate(inSceneObjects[i]);
		}

		QualitySettings.antiAliasing = antiAliasing;
		if (shadowProjectionChange == 1)
			QualitySettings.shadowProjection = ShadowProjection.StableFit;

		foreach (var pSetting in iconData.settings)
		{
			string path = AssetDatabase.GetAssetPath(pSetting.Go);
			if (path.Length <= 0)
				iconData.settings.Remove(pSetting);
		}
	}

	void OnGUI()
	{	
		//return;
		EditorGUILayout.BeginHorizontal();
		GUILayout.Space(10);

		EditorGUILayout.BeginVertical();
		BasicSettings();
		GUILayout.Space(20);

		RegistryGUI ();
		DropAreaGUI ();
		EditorGUILayout.EndVertical();

		GUILayout.Space(10);
		Preview ();
		GUILayout.Space(10);
		EditorGUILayout.EndHorizontal();
	}

	void BasicSettings()
	{
		baseSettingsUnfolded = EditorGUILayout.Foldout(baseSettingsUnfolded, "Base Settings");
		if (baseSettingsUnfolded) {
            EditorGUI.indentLevel++;
            // export folder
            EditorGUILayout.BeginHorizontal();
            exportpath = EditorGUILayout.TextField("", exportpath);
            if (GUILayout.Button("Update Path"))
            {
                exportpath = GetSelectedPathOrFallback();
				iconData.exportpath = exportpath;
            }
            EditorGUILayout.EndHorizontal();

            // image resolution
			EditorGUI.BeginChangeCheck ();
			imageWidth = EditorGUILayout.IntField ("Image width", imageWidth);
			imageHeight = EditorGUILayout.IntField ("Image Height", imageHeight);
			if (EditorGUI.EndChangeCheck ()) {
                // image resolution check
				if (imageWidth <= 0)
					imageWidth = 1;
				if (imageHeight <= 0)
					imageHeight = 1;
				iconData.imageWidth = imageWidth;
				iconData.imageHeight = imageHeight;
                RenderNow ();
			}
			// light rotation
			EditorGUI.BeginChangeCheck ();
			lightRotation = EditorGUILayout.FloatField("Light Rotate", lightRotation);
			// lighting mode
			lightingMode = EditorGUILayout.Popup ("Lighting Mode", lightingMode, lightingOptions);
            skyboxSettingsUnfolded = EditorGUILayout.Foldout(skyboxSettingsUnfolded, "Skybox Materials");
            // skybox material choices
            if (skyboxSettingsUnfolded)
            {
                EditorGUI.indentLevel++;
                skyboxNeutral = (Material)EditorGUILayout.ObjectField("Neutral", skyboxNeutral, typeof(Material), true);
                skyboxWarm = (Material)EditorGUILayout.ObjectField("Warm", skyboxWarm, typeof(Material), true);
                skyboxCold = (Material)EditorGUILayout.ObjectField("Cold", skyboxCold, typeof(Material), true);
                EditorGUI.indentLevel--;
            }
			// file format
			imageFormat = EditorGUILayout.Popup ("Image Format", imageFormat, imageOptions);

			if (EditorGUI.EndChangeCheck ()) {
				iconData.lightRotation = lightRotation;
				lightPivot.transform.rotation = Quaternion.Euler(new Vector3(0,lightRotation,0));
				// lighting mode
				if (lightingMode == 0) {
					RenderSettings.skybox = skyboxNeutral;
				} else if (lightingMode == 1) {
					RenderSettings.skybox = skyboxWarm;
				} else if (lightingMode == 2) {
					RenderSettings.skybox = skyboxCold;
				}
				iconData.lightingMode = lightingMode;
				// image format
				if (imageFormat == 0) {
					format = Format.PNG;
					_camera.backgroundColor = Color.clear;
				} else if (imageFormat == 1) {
					format = Format.JPG;
					_camera.backgroundColor = Color.white;
				} else if (imageFormat == 2) {
					format = Format.RAW;
					_camera.backgroundColor = Color.white;
				}
				iconData.imageFormat = imageFormat;
				RenderNow ();
			}
            previewSettingsUnfolded = EditorGUILayout.Foldout(previewSettingsUnfolded, "Preview");
            if (previewSettingsUnfolded)
            {
                EditorGUI.indentLevel++;
				// background
                showBackground = EditorGUILayout.Toggle("Show Background", showBackground);
                bgTexture = EditorGUILayout.ObjectField("Background Texture", bgTexture, typeof(Texture2D), true) as Texture2D;
                EditorGUI.indentLevel--;
            }
            
			EditorGUI.indentLevel--;
		}
	}

    // get selected path
    public static string GetSelectedPathOrFallback()
    {
        string path = Application.dataPath;
        foreach (UnityEngine.Object obj in Selection.GetFiltered(typeof(UnityEngine.Object), SelectionMode.Assets))
        {
            path = AssetDatabase.GetAssetPath(obj);
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                path = Path.GetDirectoryName(path);
                break;
            }
        }
        return path;
    }

	void AdvancedSettings(int i)
	{
		EditorGUILayout.BeginVertical(EditorStyles.helpBox);
		EditorGUILayout.LabelField("Advanced Settings", EditorStyles.boldLabel);
		
		EditorGUI.BeginChangeCheck ();
        prefabs[i].cameraSize = EditorGUILayout.FloatField("Camera Size", prefabs[i].cameraSize);
		prefabs[i].cameraTilt = EditorGUILayout.FloatField("Camera Tilt", prefabs[i].cameraTilt);
		prefabs[i].cameraRotate = EditorGUILayout.FloatField("Camera Rotate", prefabs[i].cameraRotate);
		if (EditorGUI.EndChangeCheck ()) {
            // update camera size
            if (prefabs[i].cameraSize < 0)
                prefabs[i].cameraSize = 0;
			// update icon data
			int index = iconData.settings.FindIndex(x => x.Go == unityGameObjects[i]);
			if (index != -1) {
				iconData.settings[index] = prefabs [i];
			}
			UpdateScene (i);
			RenderNow ();
            EditorUtility.SetDirty(iconData);
		}
		EditorGUILayout.EndVertical();
	}

	public void RegistryGUI()
	{	
		GameObject newSelected;
		bool hitDelete = false;

		for(int i=0; i<unityGameObjects.Count; i++)
		{	
			EditorGUILayout.BeginHorizontal();
            // select button
			if (GUILayout.Button ("", GUILayout.Width(20f))) {
				controlToggles[i] = !controlToggles[i];
				UpdateScene(i);
				RenderNow();
			}
			
			EditorGUI.BeginChangeCheck();
			newSelected = EditorGUILayout.ObjectField(unityGameObjects[i], typeof(GameObject), true) as GameObject;
            // change object selection
			if (EditorGUI.EndChangeCheck()) {
				if ((inSceneObjects.Count > i) && GameObject.Find(inSceneObjects[i].name))
                	DestroyImmediate(inSceneObjects[i]);
				unityGameObjects [i] = newSelected;
                inSceneObjects[i] = Instantiate(newSelected, new Vector3(0.0f, 0.0f, 0.0f), Quaternion.identity) as GameObject;
                // update icon data
				prefabs[i].Go = newSelected;
				var index = iconData.settings.FindIndex(x => x.Go == newSelected);
                // only add if one is NOT found
				if(index == -1)
				{
					iconData.settings.Add(prefabs[i]);
				}
			}
            // delete button
			if (GUILayout.Button("-", GUILayout.Width(20f))){
				unityGameObjects.RemoveAt(i);
				prefabs.RemoveAt(i);
				DestroyImmediate (inSceneObjects [i]);
				inSceneObjects.RemoveAt (i);
				controlToggles.RemoveAt(i);
				hitDelete = true;
				if (i < unityGameObjects.Count)
					UpdateScene (i);
				else if (i - 1 >= 0)
					UpdateScene (i - 1);
				if (unityGameObjects.Count == 0)
					renderTexture = null;
				RenderNow ();
			}
			EditorGUILayout.EndHorizontal();

			if ((controlToggles.Count > i) && (!hitDelete && controlToggles[i]))
				AdvancedSettings(i);
		}
	}

	public void DropAreaGUI()
	{
		var evt = Event.current;

		var dropArea = GUILayoutUtility.GetRect (0.0f, 50.0f, GUILayout.ExpandWidth (true));
		GUI.Box (dropArea, "Register New Icon");

		switch(evt.type)
		{
		case EventType.DragUpdated:
		case EventType.DragPerform:
			if (!dropArea.Contains(evt.mousePosition))
				break;

			DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

			if (evt.type == EventType.DragPerform)
			{
				DragAndDrop.AcceptDrag();
				foreach (var draggedObject in DragAndDrop.objectReferences)
				{
					var go = draggedObject as GameObject;
					if(!go)
						continue;
					unityGameObjects.Add (go);
					GameObject newObject = Instantiate (go, new Vector3 (0.0f, 0.0f, 0.0f), Quaternion.identity) as GameObject;
					// newObject.hideFlags = HideFlags.HideAndDontSave;
					PrefabSettings pSetting = new PrefabSettings();
					int index = iconData.settings.FindIndex(x => x.Go == go);
                    // load from icon data
                    if (index != -1) {
                        pSetting.Go = go;
                        pSetting.cameraSize = iconData.settings[index].cameraSize;
						pSetting.offset = iconData.settings[index].offset;
						pSetting.cameraRotate = iconData.settings[index].cameraRotate;
						pSetting.cameraTilt = iconData.settings[index].cameraTilt;
						_camTilt.transform.localRotation = Quaternion.Euler(new Vector3(pSetting.cameraTilt, 45, 0));
						_camRotate.transform.localRotation = Quaternion.Euler(new Vector3(0, pSetting.cameraRotate, 0));
                    }
                    // add to icon data
                    else {
                        Bounds bb = GetMaxBounds(newObject);
                        float max = Mathf.Max(bb.size.x, bb.size.y, bb.size.z);
                        pSetting.Go = go;
						pSetting.originalY = bb.center.y;
                        pSetting.cameraSize = max;
						pSetting.offset = new Vector2 (0, 0);
						pSetting.cameraRotate = 0.0f;
						pSetting.cameraTilt = 0.0f;
						iconData.settings.Add(pSetting);
                    }
                    controlToggles.Add(false);
                    prefabs.Add(pSetting);
                    inSceneObjects.Add(newObject);
				}
				UpdateScene (unityGameObjects.Count - 1);
				RenderNow ();
			}
			Event.current.Use();
			break;
		}
	}

	void Preview()
	{
		EditorGUILayout.BeginVertical(GUILayout.Width(previewSize), GUILayout.Height(previewSize));
		EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
        previewSize = EditorGUILayout.IntField("Preview size", previewSize);
        
        if (previewSize < 200)
            previewSize = 200;
		
		// Preview
		EditorGUILayout.BeginVertical(EditorStyles.helpBox);
		if(renderTexture != null)
		{
            // draw area
			var drawArea = GUILayoutUtility.GetRect(previewSize, previewSize, GUILayout.ExpandWidth(true));
            if (showBackground)
            {	
				if (bgTexture)
                	//var bgArea = GUILayoutUtility.GetRect(previewSize, previewSize, GUILayout.ExpandWidth(true));
                	EditorGUI.DrawPreviewTexture(drawArea, bgTexture);
            }
			GUI.DrawTexture(drawArea, renderTexture, ScaleMode.ScaleToFit);
			if ((controlIndexOnFocus >= 0) && (unityGameObjects.Count > 0)) {
                // draw slider rect
                Rect refreshRect = new Rect(drawArea.position.x + previewSize - 70, drawArea.position.y + previewSize - 30, 70, 30);
				Rect HSlider = new Rect (drawArea.position.x + 10, drawArea.position.y, buttonSize * 3, buttonSize);
				Rect VSlider = new Rect (drawArea.position.x, drawArea.position.y + 10, buttonSize, buttonSize * 3);
				EditorGUI.BeginChangeCheck ();
                float x = GUI.HorizontalSlider(HSlider, prefabs [controlIndexOnFocus].offset.x, -1, 1);
				float y = GUI.VerticalSlider(VSlider, prefabs [controlIndexOnFocus].offset.y, -1, 1, GUI.skin.verticalSlider, GUI.skin.verticalSliderThumb);

				if (EditorGUI.EndChangeCheck ()) {
					Vector2 newOffset = new Vector2(x, y);
					prefabs [controlIndexOnFocus].offset = newOffset;
				 	UpdateScene (controlIndexOnFocus);
				 	RenderNow ();
				}
                if (GUI.Button (refreshRect, "Refresh")) {
                    UpdateScene(controlIndexOnFocus);
                    RenderNow();
                }
			}
		
		}
		EditorGUILayout.EndVertical();
        // export current frame
        if (GUILayout.Button("Export Current"))
        {
            exportIcon(controlIndexOnFocus);
        }
        // export all registered prefabs
		if (GUILayout.Button("Export All")){
			for (int i = 0; i < unityGameObjects.Count; i++) {
				UpdateScene (i);
				RenderNow ();
				exportIcon (i);
			} 
		}
		EditorGUILayout.EndVertical();
	}

	/*
	void OnFocus()
	{
		camera.orthographic = true;
	}

	void OnLostFocus()
	{
		camera.orthographic = false;	
	}*/

	void UpdateScene(int i)
	{	
		if (!_camera)
			CameraSetup();
		inSceneObjects[i].SetActive(true);
		for(int j=0; j<controlToggles.Count; j++)
		{
			if (j != i){
				controlToggles[j] = false;
				inSceneObjects[j].SetActive(false);
			}
		}
		controlIndexOnFocus = i;
		_camTilt.transform.localRotation = Quaternion.Euler(new Vector3(prefabs [i].cameraTilt, 45, 0));
		_camRotate.transform.localRotation = Quaternion.Euler(new Vector3(0, prefabs[i].cameraRotate, 0));
		Vector3 newPos = new Vector3 (0 - prefabs [i].offset.x * prefabs [i].cameraSize, prefabs [i].originalY + prefabs [i].offset.y * prefabs [i].cameraSize, -10);
		_camera.orthographicSize = prefabs[i].cameraSize;
		_camera.gameObject.transform.localPosition = newPos;
	}

	void RenderNow()
	{
		if ((!renderTexture) || (renderTexture.width != imageWidth) || (renderTexture.height != imageHeight))
            renderTexture = new RenderTexture(imageWidth, imageHeight, 32, RenderTextureFormat.ARGB32);
		if (_camera != null) {
			_camera.targetTexture = renderTexture;
			_camera.Render ();
			_camera.targetTexture = null;
		}
	}

	Bounds GetMaxBounds(GameObject g) {
		var b = new Bounds(g.transform.position, Vector3.zero);
		foreach (Renderer r in g.GetComponentsInChildren<Renderer>()) {
			b.Encapsulate(r.bounds);
		}
		return b;
	}

	// create a unique filename using a one-up variable
	private string uniqueFilename(string name, int width, int height)
	{
		// use width, height, and counter for unique file name
		var filename = Path.Combine(exportpath, string.Format("UI_Icon_{0}.{1}", name, format.ToString().ToLower()));
		// return unique filename
		return filename;
	}

    private string getCurrentFolder()
    {
        string scriptFilePath;
        //string scriptFolder;
        MonoScript ms = MonoScript.FromScriptableObject(this);
        scriptFilePath = AssetDatabase.GetAssetPath(ms);
        return Path.GetDirectoryName(scriptFilePath);
    }

	private void exportIcon(int i)
	{
        if (unityGameObjects.Count < 1)
        {
            Debug.Log("Please drag a prefab to registration area.");
            return;
        }
        string name = unityGameObjects[i].name;
		Rect rect = new Rect (0, 0, imageWidth, imageHeight);

		// create screenshot objects if needed
		screenShot = new Texture2D (imageWidth, imageHeight, TextureFormat.ARGB32, false);
		RenderTexture.active = renderTexture;
		screenShot.ReadPixels(rect, 0, 0);

		string filename = uniqueFilename (name, imageWidth, imageHeight);

		// pull in our file header/data bytes for the specified image format (has to be done from main thread)
		byte[] fileHeader = null;
		byte[] fileData = null;
		if (format == Format.RAW)
		{
			fileData = screenShot.GetRawTextureData();
		}
		else if (format == Format.PNG)
		{
			fileData = screenShot.EncodeToPNG();
		}
		else if (format == Format.JPG)
		{
			fileData = screenShot.EncodeToJPG();
		}

		// create new thread to save the image to file (only operation that can be done in background)
		new System.Threading.Thread(() =>
			{
				// create file and write optional header with image bytes
				var f = System.IO.File.Create(filename);
				if (fileHeader != null) f.Write(fileHeader, 0, fileHeader.Length);
				f.Write(fileData, 0, fileData.Length);
				f.Close();
				Debug.Log(string.Format("Wrote screenshot {0} of size {1}", filename, fileData.Length));
			}).Start();

		RenderTexture.active = null;
		//renderTexture = null;
		//screenShot = null;
		AssetDatabase.Refresh();
	}
}
