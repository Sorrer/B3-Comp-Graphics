﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Collisions;
using UnityEngine;
using Random = UnityEngine.Random;

public class PrismManager : MonoBehaviour
{
    public int prismCount = 10;
    public float prismRegionRadiusXZ = 5;
    public float prismRegionRadiusY = 5;
    public float maxPrismScaleXZ = 5;
    public float maxPrismScaleY = 5;
    public GameObject regularPrismPrefab;
    public GameObject irregularPrismPrefab;

    private List<Prism> prisms = new List<Prism>();
    private List<GameObject> prismObjects = new List<GameObject>();
    private GameObject prismParent;
    private Dictionary<Prism,bool> prismColliding = new Dictionary<Prism, bool>();

    private const float UPDATE_RATE = 0.5f; // Default 0.5f

    private QuadTree _quadTree;
    private Octree _ocTree;

    public bool testOctree = true;
    public bool onlyXZ = false;
    
    private SpatialHash spatialHash;
    
    #region Unity Functions

    void Start()
    {
        _quadTree = GetComponent<QuadTree>();
        _ocTree = GetComponent<Octree>();
        
        Random.InitState(0);    //10 for no collision

        prismParent = GameObject.Find("Prisms");
        for (int i = 0; i < prismCount; i++)
        {
            var randPointCount = Mathf.RoundToInt(3 + Random.value * 7);
            var randYRot = Random.value * 360;
            var randScale = new Vector3((Random.value - 0.5f) * 2 * maxPrismScaleXZ, (Random.value - 0.5f) * 2 * maxPrismScaleY, (Random.value - 0.5f) * 2 * maxPrismScaleXZ);
            var randPos = new Vector3((Random.value - 0.5f) * 2 * prismRegionRadiusXZ, (Random.value - 0.5f) * 2 * prismRegionRadiusY, (Random.value - 0.5f) * 2 * prismRegionRadiusXZ);

            GameObject prism = null;
            Prism prismScript = null;
            if (Random.value < 0.5f)
            {
                prism = Instantiate(regularPrismPrefab, randPos, Quaternion.Euler(0, randYRot, 0));
                prismScript = prism.GetComponent<RegularPrism>();
            }
            else
            {
                prism = Instantiate(irregularPrismPrefab, randPos, Quaternion.Euler(0, randYRot, 0));
                prismScript = prism.GetComponent<IrregularPrism>();
            }
            prism.name = "Prism " + i;
            prism.transform.localScale = randScale;
            prism.transform.parent = prismParent.transform;
            prismScript.pointCount = randPointCount;
            prismScript.prismObject = prism;

            prisms.Add(prismScript);
            prismObjects.Add(prism);
            prismColliding.Add(prismScript, false);
        }

        //_quadTree.GenerateQuadTreeOfPts(prismObjects);
        //_ocTree.GenerateQuadTreeOfPts(prismObjects, ref _ocTree.root);

        StartCoroutine(Run());
    }
    
    void Update()
    {
        #region Visualization

        DrawPrismRegion();
        DrawPrismWireFrames();

#if UNITY_EDITOR
        if (Application.isFocused)
        {
            UnityEditor.SceneView.FocusWindowIfItsOpen(typeof(UnityEditor.SceneView));
        }
#endif

        #endregion
    }

    IEnumerator Run()
    {
        yield return null;
        while (true)
        {
            if (!testOctree)
            {
                _quadTree.GenerateQuadTreeOfPts(prismObjects, ref _quadTree.root);
            }
            else
            {
                _ocTree.GenerateQuadTreeOfPts(prismObjects, ref _ocTree.root);
            }
            foreach (var prism in prisms)
            {
                prismColliding[prism] = false;
            }

            foreach (var collision in PotentialCollisions())
            {
                if (CheckCollision(collision))
                {
                    prismColliding[collision.a] = true;
                    prismColliding[collision.b] = true;

                    ResolveCollision(collision);
                }
            }

            yield return new WaitForSeconds(UPDATE_RATE);
        }
    }

    #endregion

    #region Incomplete Functions

    private IEnumerable<PrismCollision> PotentialCollisions()
    {
        if (!testOctree)
        {
            for (int i = 0; i < _quadTree.leafNodes.Count; i++)
            {
                if (_quadTree.leafNodes[i].occupyingPoints.Count > 0)
                {
                    List<int> toCmpPrisms = new List<int>();
                    _quadTree.NeighbouringToCheckCells(_quadTree.leafNodes[i], ref toCmpPrisms);
                    //Debug.Log("Num Prisms to check: " + toCmpPrisms.Count);

                    PrismCollision checkPrisms = new PrismCollision();
                    int aIndex = _quadTree.leafNodes[i].occupyingPointsIndex[0];
                    checkPrisms.a = prisms[aIndex];

                    for (int j = 0; j < toCmpPrisms.Count; j++)
                    {
                        //Debug.Log("Comparing neighbours of cell : " + _quadTree.quadTreeNodes[i].ID
                        //            + " Comparing prism nums: " + toCmpPrisms[0] + " : " + toCmpPrisms[j]);
                        if (toCmpPrisms[j] != aIndex)
                        {
                            checkPrisms.b = prisms[toCmpPrisms[j]];

                            yield return checkPrisms;
                        }
                    }
                }
            }
        }
        else
        {
            for (int i = 0; i < _ocTree.leafNodes.Count; i++)
            {
                if (_ocTree.leafNodes[i].occupyingPoints.Count > 0)
                {
                    List<int> toCmpPrisms = new List<int>();
                    _ocTree.NeighbouringToCheckCells(_ocTree.leafNodes[i], ref toCmpPrisms);
                    //Debug.Log("Num Prisms to check: " + toCmpPrisms.Count);

                    PrismCollision checkPrisms = new PrismCollision();
                    int aIndex = _ocTree.leafNodes[i].occupyingPointsIndex[0];
                    checkPrisms.a = prisms[aIndex];

                    for (int j = 0; j < toCmpPrisms.Count; j++)
                    {
                        //Debug.Log("Comparing neighbours of cell : " + _quadTree.quadTreeNodes[i].ID
                        //            + " Comparing prism nums: " + toCmpPrisms[0] + " : " + toCmpPrisms[j]);
                        if (toCmpPrisms[j] != aIndex)
                        {
                            checkPrisms.b = prisms[toCmpPrisms[j]];

                            yield return checkPrisms;
                        }
                    }
                }
            }
        }

        //for (int i = 0; i < prisms.Count; i++) {
        //    for (int j = i + 1; j < prisms.Count; j++) {
        //        var checkPrisms = new PrismCollision();
        //        checkPrisms.a = prisms[i];
        //        checkPrisms.b = prisms[j];
        //
        //        yield return checkPrisms;
        //    }
        //}

        yield break;
    }

    private bool CheckCollision(PrismCollision collision)
    {
        var prismA = collision.a;
        var prismB = collision.b;

        //GJK.Has3Dimensions = Math.Abs(maxPrismScaleY) > float.Epsilon;

        Vector3[] pointsA = new Vector3[prismA.points.Length];
        Vector3[] pointsB = new Vector3[prismB.points.Length];
        
        if (onlyXZ) {
            for (int i = 0; i < prismA.points.Length; i++) {
                pointsA[i] = prismA.points[i];
                pointsA[i].y = 0;
            }
            for (int i = 0; i < prismB.points.Length; i++) {
                pointsB[i] = prismB.points[i];
                pointsB[i].y = 0;
            }
        } else {
            pointsA = prismA.points;
            pointsB = prismB.points;
        }
        
        List<Vector3> Simplex;
        bool Collided = GJK.Execute(pointsA, pointsB, out Simplex);

        if (Collided) {
            float depth;
            Vector3 normal;
            EPA.Execute(pointsA, pointsB, Simplex, out depth, out normal);
            collision.penetrationDepthVectorAB = depth * normal;
            
            //Debug.Log("Depth = " + depth);
        } else {
            collision.penetrationDepthVectorAB = Vector3.zero;
        }
        
        
        
        
        return Collided;
    }
    
    #endregion

    #region Private Functions
    
    private void ResolveCollision(PrismCollision collision)
    {
        var prismObjA = collision.a.prismObject;
        var prismObjB = collision.b.prismObject;

        var pushA = -collision.penetrationDepthVectorAB / 2;
        var pushB = collision.penetrationDepthVectorAB / 2;

        for (int i = 0; i < collision.a.pointCount; i++)
        {
            collision.a.points[i] += pushA;
        }
        for (int i = 0; i < collision.b.pointCount; i++)
        {
            collision.b.points[i] += pushB;
        }
        prismObjA.transform.position += pushA;
        prismObjB.transform.position += pushB;

        Debug.DrawLine(prismObjA.transform.position, prismObjA.transform.position + collision.penetrationDepthVectorAB, Color.cyan, UPDATE_RATE);
    }
    
    #endregion

    #region Visualization Functions

    private void DrawPrismRegion()
    {
        var points = new Vector3[] { new Vector3(1, 0, 1), new Vector3(1, 0, -1), new Vector3(-1, 0, -1), new Vector3(-1, 0, 1) }.Select(p => p * prismRegionRadiusXZ).ToArray();
        
        var yMin = -prismRegionRadiusY;
        var yMax = prismRegionRadiusY;

        var wireFrameColor = Color.yellow;

        foreach (var point in points)
        {
            Debug.DrawLine(point + Vector3.up * yMin, point + Vector3.up * yMax, wireFrameColor);
        }

        for (int i = 0; i < points.Length; i++)
        {
            Debug.DrawLine(points[i] + Vector3.up * yMin, points[(i + 1) % points.Length] + Vector3.up * yMin, wireFrameColor);
            Debug.DrawLine(points[i] + Vector3.up * yMax, points[(i + 1) % points.Length] + Vector3.up * yMax, wireFrameColor);
        }
    }

    private void DrawPrismWireFrames()
    {
        for (int prismIndex = 0; prismIndex < prisms.Count; prismIndex++)
        {
            var prism = prisms[prismIndex];
            var prismTransform = prismObjects[prismIndex].transform;

            var yMin = prism.midY - prism.height / 2 * prismTransform.localScale.y;
            var yMax = prism.midY + prism.height / 2 * prismTransform.localScale.y;

            var wireFrameColor = prismColliding[prisms[prismIndex]] ? Color.red : Color.green;

            foreach (var point in prism.points)
            {
                Debug.DrawLine(point + Vector3.up * yMin, point + Vector3.up * yMax, wireFrameColor);
            }

            for (int i = 0; i < prism.pointCount; i++)
            {
                Debug.DrawLine(prism.points[i] + Vector3.up * yMin, prism.points[(i + 1) % prism.pointCount] + Vector3.up * yMin, wireFrameColor);
                Debug.DrawLine(prism.points[i] + Vector3.up * yMax, prism.points[(i + 1) % prism.pointCount] + Vector3.up * yMax, wireFrameColor);
            }
        }
    }

    #endregion

    #region Utility Classes

    private class PrismCollision
    {
        public Prism a;
        public Prism b;
        public Vector3 penetrationDepthVectorAB;
    }

    private class Tuple<K,V>
    {
        public K Item1;
        public V Item2;

        public Tuple(K k, V v) {
            Item1 = k;
            Item2 = v;
        }
    }

    #endregion
}
