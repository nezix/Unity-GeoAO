// MIT License

// Copyright (c) 2017 Xavier Martinez

// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using UnityEngine;
using UnityEngine.Rendering;
using System.Collections;
using System.Collections.Generic;

using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.Universal;


namespace UnityGeoAO {

public class geoAO : MonoBehaviour {

    public enum samplesAOpreset  {
        VeryLow = 16,
        Low = 36,
        Medium = 64,
        High = 144,
        VeryHigh = 256,
        TooMuch = 1024,
        WayTooMuch = 2048
    }

    //Set this in the editor !
    public ForwardRendererData forwardRendererData;

    private LayerMask AOLayer;

    public samplesAOpreset samplesAO = samplesAOpreset.High;
    public bool showAOWithVertColors = false;

    public Transform meshParent;
    private MeshFilter[] mfs;
    private int[] saveLayer;
    private ShadowCastingMode[] saveShadowMode;

    private Vector3[] rayDir;

    private Bounds allBounds;

    private Camera AOCam;
    public RenderTexture AORT;
    public RenderTexture AORT2;
    private Texture2D vertTex;

    private Material AOMat;

    private int nbVert = 0;

    private int vertByRow = 256;

    private float radSurface;
    const string AOCamName = "GeoAOCam";


    void Awake()
    {

        AOLayer = 1 << LayerMask.NameToLayer("AOLayer");
        AOMat = new Material(Shader.Find("GeoAO/VertexAO"));

        var features = forwardRendererData.rendererFeatures;
        foreach (var f in features)
        {
            if (f.name == "AOBlit")
            {
                Blit feature = (Blit)f;
                Blit.BlitSettings settings = (Blit.BlitSettings)feature.settings;
                settings.blitMaterial = AOMat;
                settings.setInverseViewMatrix = true;
                settings.dstType = Blit.Target.RenderTextureObject;
                settings.cameraName = AOCamName;
                settings.requireDepth = true;
                settings.overrideGraphicsFormat = true;
                settings.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat;
            }
        }

        forwardRendererData.SetDirty();
    }

    void Start () {

        if (forwardRendererData == null) {
            Debug.LogError("Please set forwardRendererData in the editor");
            this.enabled = false;
            return;
        }

        float timerAO = Time.realtimeSinceStartup;

        nbVert = 0;
        mfs = meshParent.GetComponentsInChildren<MeshFilter>();

        List<MeshFilter> tmpMF = new List<MeshFilter>(mfs.Length);

        for (int i = 0; i < mfs.Length; i++) {
            if (mfs[i].gameObject.GetComponent<MeshRenderer>() != null) {
                nbVert += mfs[i].sharedMesh.vertexCount;
                tmpMF.Add(mfs[i]);
            }
        }
        mfs = tmpMF.ToArray();

        InitSamplePos();

        CreateAOCam();

        DoAO();

        DisplayAO();

        float timerAO2 = Time.realtimeSinceStartup;
        Debug.Log("Time for AO  = " + (timerAO2 - timerAO));
    }

    void InitSamplePos() {

        getBounds();

        radSurface = Mathf.Max(allBounds.extents.x, Mathf.Max(allBounds.extents.y, allBounds.extents.z));
        rayDir = new Vector3[(int)samplesAO];

        float golden_angle = Mathf.PI * (3 - Mathf.Sqrt(5));
        float start =  1 - 1.0f / (int)samplesAO;
        float end = 1.0f / (int)samplesAO - 1;

        for (int i = 0; i < (int)samplesAO; i++) {
            float theta = golden_angle * i;
            float z = start + i * (end - start) / (int)samplesAO;
            float radius = Mathf.Sqrt(1 - z * z);
            rayDir[i].x = radius * Mathf.Cos(theta);
            rayDir[i].y = radius * Mathf.Sin(theta);
            rayDir[i].z = z;
            rayDir[i] = allBounds.center + rayDir[i] * radSurface;
        }
    }

    void getBounds() {
        saveLayer = new int[mfs.Length];
        saveShadowMode = new ShadowCastingMode[mfs.Length];

        for (int i = 0; i < mfs.Length; i++) {
            MeshRenderer mr = mfs[i].gameObject.GetComponent<MeshRenderer>();

            saveLayer[i] = mfs[i].gameObject.layer;
            saveShadowMode[i] = mr.shadowCastingMode;

            if (i == 0)
                allBounds = mr.bounds;
            else
                allBounds.Encapsulate(mr.bounds);

            mr.shadowCastingMode = ShadowCastingMode.TwoSided;

        }
    }

    void CreateAOCam() {

        AOCam = gameObject.AddComponent<Camera>();
        if (AOCam == null)
            AOCam = gameObject.GetComponent<Camera>();

        //Set the name of the AOCamera gameobject to filter blit pass based on name
        AOCam.gameObject.name = AOCamName;

        AOCam.enabled = true;

        AOCam.orthographic = true;
        AOCam.cullingMask = 1 << LayerMask.NameToLayer("AOLayer");
        AOCam.clearFlags = CameraClearFlags.Depth;
        AOCam.nearClipPlane = 0.0001f;
        AOCam.allowHDR = false;
        AOCam.allowMSAA = false;
        AOCam.allowDynamicResolution = false;

        AOCam.depthTextureMode = DepthTextureMode.Depth ;

        AOCam.orthographicSize = radSurface * 1.1f;
        AOCam.farClipPlane = radSurface * 2;
        AOCam.aspect = 1f;



        var additionalCamData = AOCam.GetUniversalAdditionalCameraData();
        additionalCamData.renderShadows = false;
        additionalCamData.requiresColorOption = CameraOverrideOption.On;
        additionalCamData.requiresDepthOption = CameraOverrideOption.On;
        additionalCamData.renderPostProcessing = true;

        int height = (int) Mathf.Ceil(nbVert / (float)vertByRow);

        AORT = new RenderTexture(vertByRow, height, 0, RenderTextureFormat.ARGBHalf);
        AORT.anisoLevel = 0;
        AORT.filterMode = FilterMode.Point;

        AORT2 = new RenderTexture(vertByRow, height, 0, RenderTextureFormat.ARGBHalf);
        AORT2.anisoLevel = 0;
        AORT2.filterMode = FilterMode.Point;

        vertTex = new Texture2D(vertByRow, height, TextureFormat.RGBAFloat, false);
        vertTex.anisoLevel = 0;
        vertTex.filterMode = FilterMode.Point;

        //Set last Blit settings
        var features = forwardRendererData.rendererFeatures;
        foreach (var f in features)
        {
            if (f.name == "AOBlit")
            {
                Blit feature = (Blit)f;
                Blit.BlitSettings settings = (Blit.BlitSettings)feature.settings;
                settings.dstTextureObject = AORT;
            }
        }

        forwardRendererData.SetDirty();

        FillVertexTexture();
    }

    void FillVertexTexture() {
        int idVert = 0;
        int sizeRT = vertTex.width * vertTex.height;
        Color[] vertInfo = new Color[sizeRT];
        for (int i = 0; i < mfs.Length; i++) {
            Transform cur = mfs[i].gameObject.transform;
            Vector3[] vert = mfs[i].sharedMesh.vertices;
            for (int j = 0; j < vert.Length; j++) {
                Vector3 pos = cur.TransformPoint(vert[j]);
                vertInfo[idVert].r = pos.x;
                vertInfo[idVert].g = pos.y;
                vertInfo[idVert].b = pos.z;
                idVert++;
            }
        }
        vertTex.SetPixels(vertInfo);
        vertTex.Apply(false, false);
    }

    void changeAspectRatio() {
        float targetaspect = 1.0f;

        // determine the game window's current aspect ratio
        float windowaspect = (float)Screen.width / (float)Screen.height;

        // current viewport height should be scaled by this amount
        float scaleheight = windowaspect / targetaspect;


        // if scaled height is less than current height, add letterbox
        if (scaleheight < 1.0f)
        {
            Rect rect = AOCam.rect;

            rect.width = 1.0f;
            rect.height = scaleheight;
            rect.x = 0;
            rect.y = (1.0f - scaleheight) / 2.0f;

            AOCam.rect = rect;
        }
        else // add pillarbox
        {
            float scalewidth = 1.0f / scaleheight;

            Rect rect = AOCam.rect;

            rect.width = scalewidth;
            rect.height = 1.0f;
            rect.x = (1.0f - scalewidth) / 2.0f;
            rect.y = 0;

            AOCam.rect = rect;
        }

    }


    void DoAO() {


        AOMat.SetInt("_uCount", (int)samplesAO);
        AOMat.SetTexture("_AOTex", AORT);
        AOMat.SetTexture("_AOTex2", AORT2);
        AOMat.SetTexture("_uVertex", vertTex);

        for (int i = 0; i < mfs.Length; i++) {
            mfs[i].gameObject.layer = LayerMask.NameToLayer("AOLayer");
        }

        for (int i = 0; i < (int)samplesAO; i++) {

            AOCam.transform.position = rayDir[i];
            AOCam.transform.LookAt(allBounds.center);

            Matrix4x4 V = AOCam.worldToCameraMatrix;
            Matrix4x4 P = AOCam.projectionMatrix;

            bool d3d = SystemInfo.graphicsDeviceVersion.IndexOf("Direct3D") > -1;
            bool metal = SystemInfo.graphicsDeviceVersion.IndexOf("Metal") > -1;
            if (d3d || metal) {
                // Invert Y for rendering to a render texture
                for (int a = 0; a < 4; a++) {
                    P[1, a] = -P[1, a];
                }
                // Scale and bias from OpenGL -> D3D depth range
                for (int a = 0; a < 4; a++) {
                    P[2, a] = P[2, a] * 0.5f + P[3, a] * 0.5f;
                }
            }

            AOMat.SetMatrix("_VP", (P * V));
            AOMat.SetInt("_curCount", i);
            AOCam.Render();
            
            Graphics.CopyTexture(AORT, AORT2);

        }
        for (int i = 0; i < mfs.Length; i++) {
            mfs[i].gameObject.layer = saveLayer[i];
            mfs[i].gameObject.GetComponent<MeshRenderer>().shadowCastingMode = saveShadowMode[i];
        }
        AOCam.enabled = false;

    }


    void DisplayAO() {
        if (!showAOWithVertColors) { //Create a texture containing AO information read by the mesh shader
            List<Vector2[]> alluv = new List<Vector2[]>(mfs.Length);

            Material matShowAO = new Material(Shader.Find("GeoAO/VertAOOpti"));
            matShowAO.SetTexture("_AOTex", AORT);
            float w = (float)(AORT2.width - 1);
            float h = (float)(AORT2.height - 1);
            int idVert = 0;
            for (int i = 0; i < mfs.Length; i++) {
                Vector3[] vert = mfs[i].sharedMesh.vertices;
                alluv.Add( new Vector2[vert.Length] );
                for (int j = 0; j < vert.Length; j++) {
                    alluv[i][j] = new Vector2((idVert % vertByRow) / w, (idVert / (vertByRow) / h));
                    idVert++;
                }
                mfs[i].mesh.uv2 = alluv[i];//This creates a new instance of the mesh !
                mfs[i].gameObject.GetComponent<Renderer>().material = matShowAO;
            }
        }
        else { //Directly modify the colors of the mesh (slower)

            List<Color> allColors = new List<Color>(nbVert);
            RenderTexture.active = AORT2;
            Texture2D resulTex = new Texture2D(AORT2.width, AORT2.height, TextureFormat.RGBAHalf, false);
            resulTex.ReadPixels( new Rect(0, 0, AORT2.width, AORT2.height), 0, 0);

            for (int i = 0; i < nbVert; i++) {
                allColors.Add(resulTex.GetPixel(i % vertByRow, i / (vertByRow)));
            }


            int idVert = 0;
            for (int i = 0; i < mfs.Length; i++) {
                mfs[i].mesh.colors = allColors.GetRange(idVert, mfs[i].mesh.vertexCount).ToArray();//This creates a new instance of the mesh !
                idVert += mfs[i].mesh.vertexCount;
            }
        }
    }

}

}
