using UnityEngine;
using System.Collections.Generic;

public enum SyncHandler { snap, interpolate };

public class ObjectManager : MonoBehaviour
{

    public List<GameObject> tracked_objects;
    public int layer;
    public bool show_debug_messages;
    public bool show_estimated_latency;
    public bool color_synced_objects;
    public SyncHandler sync_handler;

    float time_between_sync_per_active = 0.1f;
    float time_between_sync_per_passive = 1.0f;
    float time_maximum_unsynced = 5.0f;

    float last_sync_time;
    float last_all_sync_time;

    // Use this for initialization
    void Start()
    {
        foreach (NetworkView n in GetComponents<NetworkView>())
            n.observed = this;
        tracked_objects = new List<GameObject>();
        foreach (GameObject obj in FindObjectsOfType<GameObject>())
        {
            if (obj.layer == layer)
            {
                tracked_objects.Add(obj);
            }
        }
        Debug.Log("Tracking " + tracked_objects.Count + " objects for automated networking.");
    }

    /// <summary>
    /// These variables are class fields for efficiency purposes when serialising.
    /// </summary>
    Vector3 pos;
    Quaternion rot;
    Vector3 velocity;
    Vector3 angular_velocity;

    void SyncObject(GameObject obj)
    {
        switch (sync_handler)
        {
            case SyncHandler.snap:
                obj.transform.position = pos;
                obj.rigidbody.velocity = velocity;
                obj.rigidbody.rotation = rot;
                obj.rigidbody.angularVelocity = angular_velocity;
                break;
        }
    }

    void OnSerializeNetworkView(BitStream stream, NetworkMessageInfo info)
    {
        Debug.Log(last_sync_time);
        Debug.Log(Time.time);
        if (Time.time - last_sync_time > time_between_sync_per_active)
        {
            Debug.Log("Syncing set.");
            last_sync_time = Time.time;
            foreach (GameObject obj in tracked_objects)
            {
                //if (obj.GetComponent<InterpolatedPropertySet>().hasMovedBeyondThreshold() || true)
                {
                    Debug.Log("Syncing movement data for " + obj.name);
                    if (stream.isWriting)
                    {
                        pos = obj.rigidbody.position;
                        rot = obj.rigidbody.rotation;
                        velocity = obj.rigidbody.velocity;
                        angular_velocity = obj.rigidbody.angularVelocity;

                        stream.Serialize(ref pos);
                        stream.Serialize(ref velocity);
                        stream.Serialize(ref rot);
                        stream.Serialize(ref angular_velocity);
                    }
                    else
                    {
                        stream.Serialize(ref pos);
                        stream.Serialize(ref velocity);
                        stream.Serialize(ref rot);
                        stream.Serialize(ref angular_velocity);
                        SyncObject(obj);
                    }
                }
            }
        }
    }
}
