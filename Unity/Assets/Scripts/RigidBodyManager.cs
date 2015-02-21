using UnityEngine;
using System.Collections.Generic;

public enum SyncHandler { snap, interpolate };

public class RigidBodyManager : MonoBehaviour
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
    Color color_for_sync_round;

    public void SetTrackedObject()
    {
        tracked_objects = new List<GameObject>();
        foreach (GameObject obj in FindObjectsOfType<GameObject>())
        {
            if (obj.layer == layer)
                tracked_objects.Add(obj);
        }
        if (show_debug_messages)
            Debug.Log("Tracking " + tracked_objects.Count + " objects for automated networking.");
    }

    // Use this for initialization
    void Awake()
    {
        foreach (NetworkView n in GetComponents<NetworkView>())
            n.observed = this;
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
        if(color_synced_objects)
        {
            obj.renderer.material.color = color_for_sync_round;
        }
    }

    void OnSerializeNetworkView(BitStream stream, NetworkMessageInfo info)
    {
        if (color_synced_objects)
        {
            color_for_sync_round = new Color(Random.value, Random.value, Random.value, 1.0f);
        }
        if (show_debug_messages)
        {
            Debug.Log("Last sync time: " + last_sync_time);
            Debug.Log("Current time: " + Time.time);
        }

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
