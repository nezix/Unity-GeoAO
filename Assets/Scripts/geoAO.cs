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
using System.Collections;
using System.Collections.Generic;


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

    public samplesAOpreset samplesAO = samplesAOpreset.High;

	public Transform meshParent;
	private MeshFilter[] mfs;
	private Vector3[] saveScale;
	private Transform[] saveParent;
	private Vector3[] savePos;
	private Quaternion[] saveRot;

	private Resolution saveResolution;
	private bool wasFullScreen;

	private Vector3[] rayDir;

	private Bounds allBounds;

	private Camera AOCam;
	private RenderTexture AORT;
	private RenderTexture AORT2;
	private Texture2D vertTex;

	private Material AOMat;

	private int nbVert = 0;

	private int vertByRow = 256;

	void Start () {

		float timerAO = Time.realtimeSinceStartup;

		nbVert = 0;
		mfs = meshParent.GetComponentsInChildren<MeshFilter>();
		for(int i=0;i<mfs.Length;i++)
			nbVert += mfs[i].mesh.vertices.Length;

		InitSamplePos();

		CreateAOCam();

		DoAO();

		DisplayAO();

		float timerAO2 = Time.realtimeSinceStartup;
        Debug.Log("Time for AO  = "+(timerAO2 - timerAO));
	}

	void InitSamplePos(){

		getBounds();

		Vector3 boundMax = allBounds.max;
		float radSurface = Mathf.Max(boundMax.x,Mathf.Max(boundMax.y,boundMax.z));
		rayDir = new Vector3[(int)samplesAO];

		float golden_angle = Mathf.PI * (3 - Mathf.Sqrt(5));
		float start =  1 - 1.0f/(int)samplesAO;
		float end = 1.0f/(int)samplesAO - 1;

		for(int i=0;i<(int)samplesAO;i++){
			float theta = golden_angle * i;
			float z = start+ i*(end-start)/(int)samplesAO;
			float radius = Mathf.Sqrt(1- z*z);
			rayDir[i].x = radius * Mathf.Cos(theta);
			rayDir[i].y = radius * Mathf.Sin(theta);
			rayDir[i].z = z;
			rayDir[i] *= radSurface;
			rayDir[i] += allBounds.center;

			// Debug
			// GameObject test = GameObject.CreatePrimitive(PrimitiveType.Cube);
			// test.transform.localScale = Vector3.one *1f;
			// test.transform.position = rayDir[i];
			// test.transform.parent = bigParent.transform;
		}
	}

	void getBounds(){
		saveScale = new Vector3[mfs.Length];
		saveParent = new Transform[mfs.Length];
		savePos = new Vector3[mfs.Length];
		saveRot = new Quaternion[mfs.Length];

		for(int i=0;i<mfs.Length;i++){
			saveScale[i] = mfs[i].transform.localScale;
			saveParent[i] = mfs[i].transform.parent;
			savePos[i] = mfs[i].transform.position;
			saveRot[i] = mfs[i].transform.rotation;
			if(i==0)
				allBounds = mfs[i].mesh.bounds;
			else
				allBounds.Encapsulate(mfs[i].mesh.bounds);

		}
	}

	void CreateAOCam(){

		AOCam = gameObject.AddComponent<Camera>();
		if(AOCam == null)
			AOCam = gameObject.GetComponent<Camera>();


        AOCam.enabled=false;

        // AOCam.CopyFrom(Camera.main);
        AOCam.orthographic = true;
        AOCam.cullingMask=1<<0; // default layer for now
        AOCam.clearFlags = CameraClearFlags.Depth;
        AOCam.nearClipPlane = 1f;
        AOCam.farClipPlane = 500f;

		AOCam.depthTextureMode = DepthTextureMode.Depth ;

		saveResolution = Screen.currentResolution;
		wasFullScreen = Screen.fullScreen;

		changeAspectRatio();

        float screenRatio = 1f;

        float targetRatio = allBounds.size.x / allBounds.size.y;

        if (screenRatio >= targetRatio)
        	AOCam.orthographicSize = 1.1f * (allBounds.size.y / 2);
        else {
            float differenceInSize = targetRatio / screenRatio;
            AOCam.orthographicSize = 1.1f*(allBounds.size.y / 2 * differenceInSize);
        }

        AOMat = new Material(Shader.Find("Custom/VertexAO"));


        int height = (int) Mathf.Ceil(nbVert/(float)vertByRow);

        Debug.Log("Creating a texture of size : "+vertByRow+" x "+height);
        Debug.Log("Vertices = "+nbVert);

        AORT = new RenderTexture(vertByRow,height,0,RenderTextureFormat.ARGBHalf);
        AORT.anisoLevel = 0;
        AORT.filterMode = FilterMode.Point;

        AORT2 = new RenderTexture(vertByRow,height,0,RenderTextureFormat.ARGBHalf);
        AORT2.anisoLevel = 0;
        AORT2.filterMode = FilterMode.Point;

        vertTex = new Texture2D(vertByRow,height,TextureFormat.RGBAFloat,false);
        vertTex.anisoLevel = 0;
        vertTex.filterMode = FilterMode.Point;

       	FillVertexTexture();
	}

	void FillVertexTexture(){
		int idVert = 0;
		int sizeRT = vertTex.width * vertTex.height;
		Color[] vertInfo = new Color[sizeRT];
		for(int i=0;i<mfs.Length;i++){
			Vector3[] vert = mfs[i].mesh.vertices;
			for(int j=0;j<vert.Length;j++){
				vertInfo[idVert].r = vert[j].x;
				vertInfo[idVert].g = vert[j].y;
				vertInfo[idVert].b = vert[j].z;
				idVert++;
			}
		}
		vertTex.SetPixels(vertInfo);
        vertTex.Apply(false,false);
	}

	void changeAspectRatio(){
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


	void DoAO(){


        AOMat.SetInt("_uCount",(int)samplesAO);
        AOMat.SetTexture("_AOTex",AORT);
        AOMat.SetTexture("_AOTex2",AORT2);
        AOMat.SetTexture("_uVertex",vertTex);

        for(int i=0;i<mfs.Length;i++){
        	mfs[i].transform.parent = null;
        	mfs[i].transform.position = Vector3.zero;
        	mfs[i].transform.rotation = Quaternion.Euler(0f,0f,0f);
        	mfs[i].transform.localScale = Vector3.one;
        }

		for(int i=0;i<(int)samplesAO;i++){

        	AOCam.transform.position = rayDir[i];
        	AOCam.transform.LookAt(allBounds.center);

        	//Not sure if necessay ?
				Matrix4x4 V = AOCam.worldToCameraMatrix;
				Matrix4x4 P = AOCam.projectionMatrix;

				bool d3d = SystemInfo.graphicsDeviceVersion.IndexOf("Direct3D") > -1;
				if (d3d) {
				    // Invert Y for rendering to a render texture
				    for (int a = 0; a < 4; a++) {
				        P[1,a] = -P[1,a];
				    }
				    // Scale and bias from OpenGL -> D3D depth range
				    for (int a = 0; a < 4; a++) {
				        P[2,a] = P[2,a]*0.5f + P[3,a]*0.5f;
				    }
				}

        	AOMat.SetMatrix("_VP",(P*V));
        	AOCam.Render();
        }
        for(int i=0;i<mfs.Length;i++){
        	mfs[i].transform.parent = saveParent[i];
        	mfs[i].transform.position = savePos[i];
        	mfs[i].transform.localScale = saveScale[i];
        	mfs[i].transform.rotation = saveRot[i];
        }

	}
	void OnRenderImage (RenderTexture source, RenderTexture destination) {


		var matrix = AOCam.cameraToWorldMatrix;
		AOMat.SetMatrix("_InverseView",matrix);
		Graphics.Blit(null, AORT, AOMat);
		AOCam.targetTexture = null;
		Graphics.Blit(AORT,AORT2);
	}

	void DisplayAO(){

		if(true){//Create a texture containing AO information read by the mesh shader
			List<Vector2[]> alluv = new List<Vector2[]>(mfs.Length);

			Material matShowAO = new Material(Shader.Find("AO/VertAOOpti"));
			matShowAO.SetTexture("_AOTex",AORT);
			float w = (float)(AORT2.width-1);
			float h =(float)(AORT2.height-1);
			int idVert = 0;
			for(int i=0;i<mfs.Length;i++){
				Vector3[] vert = mfs[i].mesh.vertices;
				alluv.Add( new Vector2[vert.Length] );
				for(int j=0;j<vert.Length;j++){
					alluv[i][j] = new Vector2((idVert%vertByRow)/w,(idVert/(vertByRow)/h));
					idVert++;
				}
				mfs[i].mesh.uv2 = alluv[i];
				mfs[i].gameObject.GetComponent<Renderer>().material = matShowAO;
			}
		}
		else{//Directly modify the colors of the mesh (slower)

			List<Color> allColors = new List<Color>(nbVert);
			RenderTexture.active = AORT2;
			Texture2D resulTex = new Texture2D(AORT2.width,AORT2.height,TextureFormat.RGBAHalf,false);
			resulTex.ReadPixels( new Rect(0, 0, AORT2.width,AORT2.height), 0, 0);

			for(int i=0;i<nbVert;i++){
				allColors.Add(resulTex.GetPixel(i%vertByRow,i/(vertByRow)));
			}


			int idVert = 0;
			for(int i=0;i<mfs.Length;i++){
				mfs[i].mesh.colors = allColors.GetRange(idVert,mfs[i].mesh.vertices.Length).ToArray();
				idVert += mfs[i].mesh.vertices.Length;
			}
		}
	}

}

