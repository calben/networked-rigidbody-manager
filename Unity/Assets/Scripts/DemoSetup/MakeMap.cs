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

    public void PlaceCubes()
    {
        cubecolor = new Color(Random.value, Random.value, Random.value, 1.0f);
        for (int i = 0; i < numberOfCubes; i++)
        {
            GameObject c = (GameObject) Network.Instantiate(cube, GeneratedPosition(), Quaternion.identity, 1);
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
