using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using System.Reflection;

public class Framedump : EditorWindow
{
    // folder to write output (defaults to data path)
    private string exportpath;
    private string sequencePath;
    private string ffmpegPath;
    // target prefab
    public GameObject target;
    // frame rate
    public int frameRate = 30;
    // length
    public int length = 30;
    public int count = 0;
    // in scene objects
    public GameObject currentGO;
    public bool isPlaying = false;
    // image sequence list
    public List<string> dirs = new List<string>();
    // format
    public enum Format { GIF, MOV };
    public string[] videoOptions = new string[]{"gif", "mov"};
	public int videoFormat = 0;
	public Format format = Format.GIF;
    private RenderTexture renderTexture;
    private Texture2D screenShot;
    private int imageWidth = 256;
    private int imageHeight = 256;
    private bool isEnabled = false;

    // Add menu named "Tools" to the Window menu
    [MenuItem("Tools/Framedump")]
    static void Init()
    {
        // Get existing open window or if none, make a new one:
        Framedump window = (Framedump)EditorWindow.GetWindow(typeof(Framedump));
        window.Show();
    }

    void OnEnable()
    {
        if (!isEnabled) {
            // check pre-defined export folder
            exportpath = Application.dataPath;
            Time.captureFramerate = frameRate;
            // ffmpeg path
            ffmpegPath = Path.Combine(Application.dataPath, "ThirdParty");
            ffmpegPath = Path.Combine(ffmpegPath, "ffmpeg");
            // check ffmpeg existence
            if (!File.Exists(Path.Combine(ffmpegPath, "ffmpeg.exe"))) {
                Debug.Log(Path.Combine(ffmpegPath, "ffmpeg.exe") + " does not exist!");
                return;
            }
            // render texture
            renderTexture = new RenderTexture(imageHeight, imageWidth, 32, RenderTextureFormat.ARGB32);
            isEnabled = true;
        }
    }

    void OnDestroy()
    {
        if (currentGO != null)
            DestroyImmediate(currentGO);
        for (int i = 0; i < dirs.Count; i++)
        {
            if (Directory.Exists(dirs[i]))
                FileUtil.DeleteFileOrDirectory(dirs[i]);
        }
    }

    void OnGUI()
    {
        //return;
        EditorGUILayout.BeginVertical();
        // tip
        EditorGUILayout.LabelField("Tips:");
        EditorGUILayout.LabelField("    Adjust Main Camera position to achieve a preferred view.");
        EditorGUILayout.LabelField("    50 frames per second is highest quality for common browsers.");
        GUILayout.Space(10);
        // GUI
        drawGUI();
        EditorGUILayout.EndVertical();
    }

    void drawGUI()
    {   
        // export folder
        EditorGUILayout.BeginHorizontal();
        exportpath = EditorGUILayout.TextField("", exportpath);
        if (GUILayout.Button("Update Path"))
        {
            exportpath = GetSelectedPathOrFallback();
        }
        EditorGUILayout.EndHorizontal();
        // image size
        EditorGUI.BeginChangeCheck ();
        imageWidth = EditorGUILayout.IntField ("Image width", imageWidth);
		imageHeight = EditorGUILayout.IntField ("Image Height", imageHeight);
		if (EditorGUI.EndChangeCheck ()) {
            // image resolution check
			if (imageWidth <= 0)
				imageWidth = 1;
			if (imageHeight <= 0)
				imageHeight = 1;
            RenderNow();
        }
        // frame rate
        EditorGUI.BeginChangeCheck ();
        frameRate = EditorGUILayout.IntField ("Frame Rate", frameRate);
        if (EditorGUI.EndChangeCheck ()) {
            Time.captureFramerate = frameRate;
        }
        
        // length
        length = EditorGUILayout.IntField ("Record frames", length);
        if (length < 1)
            length = 1;
        // file format
        EditorGUI.BeginChangeCheck ();
		videoFormat = EditorGUILayout.Popup ("Video Format", videoFormat, videoOptions);
        if (EditorGUI.EndChangeCheck ()) {
            // image format
            if (videoFormat == 0) {
                format = Format.GIF;
            } else if (videoFormat == 1) {
                format = Format.MOV;
            }
        }
        // target object
        GameObject newTarget;
        EditorGUI.BeginChangeCheck ();
        newTarget = EditorGUILayout.ObjectField(target, typeof(GameObject), true) as GameObject;
        if (EditorGUI.EndChangeCheck ()) {
            if (newTarget != null && PrefabUtility.GetPrefabType(newTarget) == PrefabType.Prefab) {
                target = newTarget;
                if (target != null)
                    // sequence path
                    sequencePath = Path.Combine(ffmpegPath, target.name);
            }
            replaceCurrentGO(target);
        }
        // record
        GUILayout.Space(10);
        if (GUILayout.Button("Start Recording"))
        {
            startRecord(target);
        }
    }

    GameObject replaceCurrentGO(GameObject target)
    {
        if (currentGO != null)
            DestroyImmediate(currentGO);
        currentGO = Instantiate(target, new Vector3(0.0f, 0.0f, 0.0f), Quaternion.identity) as GameObject;
        Rigidbody rb = currentGO.GetComponent<Rigidbody>();
        if (rb != null){
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }
        return currentGO;
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
        path = Path.Combine(Path.GetDirectoryName(Application.dataPath), path);
        return path;
    }
    
    void startRecord(GameObject target)
    {
        if (target != null) {
            // check if dir exists
            if (Directory.Exists(sequencePath)) {
                FileUtil.DeleteFileOrDirectory(sequencePath);
            }
            else
                dirs.Add(sequencePath);
            if (isPlaying == false) {
                EditorApplication.isPlaying = true;
                isPlaying = true;
                // image sequences path
                System.IO.Directory.CreateDirectory(sequencePath);
            }
        }
        else
            return;
    }

    void endRecord()
    {
        EditorApplication.isPlaying = false;
        isPlaying = false;
        // check if file exists
        string fileFormat = format.ToString().ToLower();
        string videoFilePath = string.Format("{0}/{1}.{2}", exportpath, target.name + "_" + fileFormat, fileFormat);
        System.IO.Directory.CreateDirectory(exportpath);
        if (System.IO.File.Exists(videoFilePath))
        {
            File.Delete(videoFilePath);
        }
        // generate video
        System.Diagnostics.Process process = new System.Diagnostics.Process();
        System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
        startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
        startInfo.WorkingDirectory = @ffmpegPath;
        startInfo.FileName = "cmd.exe";
        if (videoFormat == 0)
            startInfo.Arguments = string.Format("/c ffmpeg -r {0} -i {1}/{2}%04d.png -pix_fmt yuv420p -vf fps={3} {4}", frameRate, sequencePath, target.name, frameRate, videoFilePath);
        else
            startInfo.Arguments = string.Format("/c ffmpeg -framerate {0} -i {1}/{2}%04d.png -c:v libx264 -pix_fmt yuv420p -acodec copy -vcodec copy -f {3} {4}", frameRate, sequencePath, target.name, fileFormat, videoFilePath);
            //startInfo.Arguments = string.Format("/c ffmpeg -framerate {0} -i {1}/{2}%04d.png -pix_fmt yuv420p  -f {3} {4}", frameRate, sequencePath, target.name, fileFormat, videoFilePath);
        process.StartInfo = startInfo;
        process.Start();
        count = 0;
        Debug.Log("Exported at path: " + videoFilePath);

    }

    void Update()
    {   
        if (isPlaying)
        {   
            if (count < length) {
                // Append filename to folder name (format is 'assetname0005.png"')
                string name = string.Format("{0}/{1}{2:D04}.png", sequencePath, target.name, count + 1);
                // Capture the screenshot to the specified file.
                RenderNow();
                exportIcon(name);
                count++;
            }
            else {
                endRecord();
            }
        }
    }

    private void RenderNow()
    {
        if ((!renderTexture) || (renderTexture.width != imageWidth) || (renderTexture.height != imageHeight))
            renderTexture = new RenderTexture(imageWidth, imageHeight, 32, RenderTextureFormat.ARGB32);
		if (Camera.main != null) {
			Camera.main.targetTexture = renderTexture;
			Camera.main.Render ();
			Camera.main.targetTexture = null;
		}
    }

    private void exportIcon(string filename)
	{
		Rect rect = new Rect (0, 0, imageHeight, imageWidth);

		// create screenshot objects if needed
		screenShot = new Texture2D (imageHeight, imageWidth, TextureFormat.ARGB32, false);
		RenderTexture.active = renderTexture;
		screenShot.ReadPixels(rect, 0, 0);

		// pull in our file header/data bytes for the specified image format (has to be done from main thread)
		byte[] fileHeader = null;
		byte[] fileData = null;
		fileData = screenShot.EncodeToPNG();

		// create new thread to save the image to file (only operation that can be done in background)
		new System.Threading.Thread(() =>
			{
				// create file and write optional header with image bytes
				var f = System.IO.File.Create(filename);
				if (fileHeader != null) f.Write(fileHeader, 0, fileHeader.Length);
				f.Write(fileData, 0, fileData.Length);
				f.Close();
				// Debug.Log(string.Format("Wrote screenshot {0} of size {1}", filename, fileData.Length));
			}).Start();

		RenderTexture.active = null;
		AssetDatabase.Refresh();
	}

    private string getCurrentFolder()
    {
        string scriptFilePath;
        MonoScript ms = MonoScript.FromScriptableObject(this);
        scriptFilePath = AssetDatabase.GetAssetPath(ms);
        return Path.GetDirectoryName(scriptFilePath);
    }
}