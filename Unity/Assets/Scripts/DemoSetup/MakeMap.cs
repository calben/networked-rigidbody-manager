using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class MakeMap : MonoBehaviour
{

    public GameObject cube;
    public int numberOfCubes;
    public int min, max;
    public string layer;
    public Color cubecolor;

    void Start()
    {
        PlaceCubes();
    }

    void PlaceCubes()
    {
        for (int i = 0; i < numberOfCubes; i++)
        {
            GameObject c = (GameObject) Instantiate(cube, GeneratedPosition(), Quaternion.identity);
            c.renderer.material.color = cubecolor;
            c.layer = LayerMask.NameToLayer(layer);
        }
        GameObject.Find("RigidBodyManager").GetComponent<RigidBodyManager>().SetTrackedObject();
    }
    Vector3 GeneratedPosition()
    {
        int x, y, z;
        x = UnityEngine.Random.Range(min, max);
        y = UnityEngine.Random.Range(min, max);
        z = UnityEngine.Random.Range(min, max);
        return new Vector3(x, y, z);
    }

}
