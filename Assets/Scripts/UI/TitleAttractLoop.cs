using System.Collections.Generic;
using CompanyF.Track;
using UnityEngine;

namespace CompanyF.UI
{
    /// <summary>Title-only train tour. Gameplay scripts remain disabled; all geometry proxies are transient.</summary>
    [DefaultExecutionOrder(500)]
    public sealed class TitleAttractLoop : MonoBehaviour
    {
        [SerializeField] Transform locomotive;
        [SerializeField] Camera filmingCamera;
        [SerializeField] TrackRoute route;
        [SerializeField] RectTransform titleCanvas;
        [SerializeField] TutorialVideoController tutorial;
        [SerializeField, Min(1)] float travelSpeed = 18f;
        [SerializeField, Min(2)] float shotDuration = 6f;
        const int GeometryLayer = 31;
        const int GeometryMask = 1 << GeometryLayer;
        const float Clearance = 0.4f;

        static readonly Vector3[] Starts = {
            new Vector3(10,6,12), new Vector3(-14,5,1),
            new Vector3(9,7,-13), new Vector3(-10,12,-9)
        };
        static readonly Vector3[] Ends = {
            new Vector3(12,5,9), new Vector3(-12,4,-3),
            new Vector3(11,6,-10), new Vector3(-7,10,-12)
        };
        static readonly Vector3[] VisibilityPoints = { new Vector3(0,1.9f,5),new Vector3(0,2,-4.5f) };
        readonly List<TrackSegment> segments = new List<TrackSegment>();
        readonly List<float> distances = new List<float>();
        readonly List<GameObject> geometry = new List<GameObject>();
        UnityEngine.UI.Image fade;
        float length, time;
        int previousShot = -1, variant;
        bool initialized;
        Bounds filmingCorridor;

        public int ShotIndex { get; private set; }
        public int LoopCount { get; private set; }
        public float RouteDistance { get; private set; }
        public Vector3 Focus { get; private set; }
        public Camera FilmingCamera => filmingCamera;
        public Transform Locomotive => locomotive;
        public int GeometryCount => geometry.Count;

        void Awake()
        {
            if (locomotive == null || filmingCamera == null || route == null || route.start == null)
            { Debug.LogError("[Title Attract] Missing scene references", this); enabled=false; return; }
            foreach(var behaviour in locomotive.GetComponentsInChildren<MonoBehaviour>(true)) if(behaviour!=null)behaviour.enabled=false;
            foreach(var camera in locomotive.GetComponentsInChildren<Camera>(true)) camera.enabled=false;
            foreach(var listener in locomotive.GetComponentsInChildren<AudioListener>(true)) listener.enabled=false;
            foreach(var audio in locomotive.GetComponentsInChildren<AudioSource>(true)) { audio.Stop(); audio.enabled=false; }
            foreach(var body in locomotive.GetComponentsInChildren<Rigidbody>(true)) body.isKinematic=true;
            foreach(var t in FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(t.CompareTag("Cow") || t.CompareTag("Horse")) t.gameObject.SetActive(false);
            locomotive.gameObject.SetActive(true);
            foreach(var camera in FindObjectsByType<Camera>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                camera.enabled = camera == filmingCamera;
            filmingCamera.nearClipPlane=0.08f;
            filmingCamera.fieldOfView=48f;

            var visited = new HashSet<TrackSegment>();
            for(var segment=route.start;segment!=null && visited.Add(segment);segment=segment.next)
            { distances.Add(length);segments.Add(segment);length+=segment.Length; }
            if(length<30f) { Debug.LogError("[Title Attract] Track is too short",this);enabled=false;return; }
            filmingCorridor=new Bounds(PointAt(0),Vector3.zero);
            foreach(var segment in segments)
                foreach(var point in segment.Points)filmingCorridor.Encapsulate(segment.transform.TransformPoint(point));
            filmingCorridor.Expand(new Vector3(64,20000,64));
            BuildGeometry();
            CreateFade();
            initialized=true;
            RenderAt(0);
            Debug.Log($"[Title Attract] initialized: {length:F1} m, {geometry.Count} geometry proxies, 4 shots",this);
        }

        void CreateFade()
        {
            if(titleCanvas==null)return;
            var background=titleCanvas.Find("Background");
            if(background!=null && background.TryGetComponent<UnityEngine.UI.Image>(out var image))image.enabled=false;
            var go=new GameObject("AttractLoopFade",typeof(RectTransform),typeof(UnityEngine.UI.Image));
            var rect=(RectTransform)go.transform;
            rect.SetParent(titleCanvas,false);rect.SetAsFirstSibling();
            rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            fade=go.GetComponent<UnityEngine.UI.Image>();fade.raycastTarget=false;fade.color=Color.clear;
        }

        void BuildGeometry()
        {
            // Include visible environment meshes even when their art prefabs have no colliders.
            foreach(var filter in FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
            {
                if(filter.sharedMesh==null || filter.transform.IsChildOf(locomotive))continue;
                if(!filter.TryGetComponent<Renderer>(out var renderer) || !renderer.enabled)continue;
                if(!filmingCorridor.Intersects(renderer.bounds))continue;
                var proxy=NewProxy(filter.transform);
                proxy.AddComponent<MeshCollider>().sharedMesh=filter.sharedMesh;
            }
            foreach(var terrain in Terrain.activeTerrains)
            {
                var proxy=NewProxy(terrain.transform);
                proxy.AddComponent<TerrainCollider>().terrainData=terrain.terrainData;
                var data=terrain.terrainData;
                var radii=new float[data.treePrototypes.Length];
                for(int i=0;i<radii.Length;i++)
                {
                    var model=data.treePrototypes[i].prefab;
                    if(model==null)continue;
                    foreach(var filter in model.GetComponentsInChildren<MeshFilter>(true))
                    {
                        if(filter.sharedMesh==null)continue;
                        var bounds=filter.sharedMesh.bounds;
                        for(int corner=0;corner<8;corner++)
                        {
                            var p=bounds.center+Vector3.Scale(bounds.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1));
                            p=model.transform.InverseTransformPoint(filter.transform.TransformPoint(p));
                            p=Vector3.Scale(p,model.transform.localScale);
                            radii[i]=Mathf.Max(radii[i],new Vector2(p.x,p.z).magnitude);
                        }
                    }
                }
                foreach(var tree in data.treeInstances)
                {
                    var prefab=data.treePrototypes[tree.prototypeIndex].prefab;
                    if(prefab==null)continue;
                    var position=terrain.transform.TransformPoint(Vector3.Scale(tree.position,data.size));
                    float radius=radii[tree.prototypeIndex]*tree.widthScale;
                    if(position.x+radius<filmingCorridor.min.x || position.x-radius>filmingCorridor.max.x
                        || position.z+radius<filmingCorridor.min.z || position.z-radius>filmingCorridor.max.z)continue;
                    var treeRoot=NewProxy(transform);
                    treeRoot.transform.position=position;
                    treeRoot.transform.rotation=Quaternion.Euler(0,tree.rotation*Mathf.Rad2Deg,0);
                    treeRoot.transform.localScale=Vector3.Scale(new Vector3(tree.widthScale,tree.heightScale,tree.widthScale),prefab.transform.localScale);
                    foreach(var mesh in prefab.GetComponentsInChildren<MeshFilter>(true))
                    {
                        if(mesh.sharedMesh==null)continue;
                        var shape=NewProxy(treeRoot.transform);
                        var matrix=prefab.transform.worldToLocalMatrix*mesh.transform.localToWorldMatrix;
                        shape.transform.localPosition=matrix.GetColumn(3);
                        shape.transform.localRotation=matrix.rotation;
                        shape.transform.localScale=matrix.lossyScale;
                        shape.AddComponent<MeshCollider>().sharedMesh=mesh.sharedMesh;
                    }
                }
            }
            Physics.SyncTransforms();
        }

        GameObject NewProxy(Transform parent)
        {
            var go=new GameObject("_TitleCameraGeometry") {hideFlags=HideFlags.HideAndDontSave,layer=GeometryLayer};
            go.transform.SetParent(parent,false);geometry.Add(go);return go;
        }

        void LateUpdate()
        {
            if(!initialized || (tutorial!=null && tutorial.Stage!=TutorialVideoController.PlaybackStage.Idle))return;
            time+=Time.unscaledDeltaTime;
            RenderAt(time);
        }

        Vector3 PointAt(float distance)
        {
            int index=segments.Count-1;
            while(index>0 && distance<distances[index])index--;
            float local=distance-distances[index];var segment=segments[index];
            segment.Evaluate(local,out var p,out var forward);
            if(local<0)p+=forward*local;
            if(local>segment.Length)p+=forward*(local-segment.Length);
            return p;
        }

        /// <summary>Deterministic pose sampling also used by the full-route camera verification.</summary>
        public void RenderAt(float elapsed)
        {
            if(!initialized)return;
            float run=length-26f;
            float travelled=Mathf.Max(0,elapsed)*travelSpeed;
            LoopCount=Mathf.FloorToInt(travelled/run);
            RouteDistance=13f+Mathf.Repeat(travelled,run);
            var front=PointAt(RouteDistance+4.34f);var rear=PointAt(RouteDistance-2.14f);
            var forward=(front-rear).normalized;
            locomotive.SetPositionAndRotation(rear+forward*2.14f,Quaternion.LookRotation(forward,Vector3.up));
            Focus=locomotive.TransformPoint(0,2.25f,0.3f);
            ShotIndex=Mathf.FloorToInt(elapsed/shotDuration)%Starts.Length;
            if(ShotIndex!=previousShot) { variant=0;previousShot=ShotIndex; }
            float progress=Mathf.SmoothStep(0,1,Mathf.Repeat(elapsed,shotDuration)/shotDuration);
            var desired=Vector3.Lerp(Starts[ShotIndex],Ends[ShotIndex],progress);
            filmingCamera.transform.position=FindSafePosition(desired);
            var viewing=Focus-filmingCamera.transform.position;
            var basis=Quaternion.LookRotation(viewing,Vector3.up);
            float halfHeight=viewing.magnitude*Mathf.Tan(filmingCamera.fieldOfView*0.5f*Mathf.Deg2Rad);
            // Keep the real locomotive below and to the right of the existing title logo.
            var aim=Focus+basis*new Vector3(-halfHeight*filmingCamera.aspect*0.38f,halfHeight*0.48f,0);
            filmingCamera.transform.rotation=Quaternion.LookRotation(aim-filmingCamera.transform.position,Vector3.up);
            if(fade!=null)
            {
                float secondsToWrap=(run-Mathf.Repeat(travelled,run))/travelSpeed;
                float secondsAfterWrap=Mathf.Repeat(travelled,run)/travelSpeed;
                float opacity=Mathf.Max(1-Mathf.Clamp01(secondsToWrap/0.65f),LoopCount>0?1-Mathf.Clamp01(secondsAfterWrap/0.65f):0);
                fade.color=new Color(0,0,0,opacity);
            }
        }

        Vector3 Candidate(Vector3 offset,int option)
        {
            if((option&1)!=0)offset.x=-offset.x;
            offset.y+=(option/2)*3f;
            return locomotive.TransformPoint(offset);
        }

        public bool CameraPositionClear(Vector3 position)
        {
            if(Physics.CheckSphere(position,Clearance,GeometryMask,QueryTriggerInteraction.Ignore))return false;
            var delta=position-Focus;
            if(Physics.SphereCast(Focus,Clearance,delta.normalized,out _,delta.magnitude,GeometryMask,QueryTriggerInteraction.Ignore))return false;
            foreach(var local in VisibilityPoints)
            {
                var point=locomotive.TransformPoint(local);delta=position-point;
                if(Physics.SphereCast(point,0.2f,delta.normalized,out _,delta.magnitude,GeometryMask,QueryTriggerInteraction.Ignore))return false;
            }
            return true;
        }

        Vector3 FindSafePosition(Vector3 desired)
        {
            var candidate=Candidate(desired,variant);
            if(CameraPositionClear(candidate))return candidate;
            for(int option=0;option<16;option++)
            {
                candidate=Candidate(desired,option);
                if(!CameraPositionClear(candidate))continue;
                variant=option;return candidate;
            }
            // A wide fan handles stations, canyon walls, and future building placements.
            for(float height=4;height<=40;height+=4)
                for(int angle=0;angle<8;angle++)
                {
                    var offset=Quaternion.Euler(0,angle*45,0)*new Vector3(0,height,13);
                    candidate=locomotive.TransformPoint(offset);
                    if(CameraPositionClear(candidate))return candidate;
                }
            return filmingCamera.transform.position;
        }

        void OnDestroy()
        {
            foreach(var proxy in geometry)if(proxy!=null)Destroy(proxy);
        }
    }
}
