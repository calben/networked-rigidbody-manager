using UnityEngine;
using System.Collections.Generic;

public enum SyncHandler { snap, interpolate };

public class RigidBodyManager : MonoBehaviour
{

    public List<GameObject> TrackedObject;
    List<GameObject> PlayerPrioritizedObjects;

    public int layer;
    public bool isShowingDebug;
    public bool isShowingLatency;
    public bool isColoringPerSync;
    public bool isColoringByPlayerProximity;
    public SyncHandler syncHandler;

    public bool prioritizeByPlayerDistance;
    public float playerProximity;

    public float timeBetweenSyncHighPriority = 0.1f;
    public float timeBetweenSyncLowPriority = 1.0f;
    public float timeMaximumUnsynced = 5.0f;

    float timeLastHighPrioritySync;
    float timeLastLowPrioritySync;
    float timeLastAllSynced;
    public Color syncedRoundColor;
    public Color prioritizedColor = Color.yellow;

    public void ResetTrackedObjects()
    {
        TrackedObject = new List<GameObject>();
        foreach (GameObject obj in FindObjectsOfType<GameObject>())
        {
            if (obj.layer == layer)
            {
                TrackedObject.Add(obj);
                if(isColoringPerSync)
                {
                    obj.renderer.material.color = Color.green;
                }
            }
        }
        if (isShowingDebug)
            Debug.Log("Tracking " + TrackedObject.Count + " objects for automated networking.");
    }

    // Use this for initialization
    void Awake()
    {
        foreach (NetworkView n in GetComponents<NetworkView>())
            n.observed = this;
        PlayerPrioritizedObjects = new List<GameObject>();
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
        switch (syncHandler)
        {
            case SyncHandler.snap:
                obj.transform.position = pos;
                obj.rigidbody.velocity = velocity;
                obj.rigidbody.rotation = rot;
                obj.rigidbody.angularVelocity = angular_velocity;
                break;
        }
        if (isColoringPerSync)
        {
            obj.renderer.material.color = syncedRoundColor;
        }
    }

    void SetPlayerPriorityObjects()
    {
        PlayerPrioritizedObjects.Clear();
        foreach (GameObject player in GameObject.FindGameObjectsWithTag("Player"))
        {
            Vector3 player_position = player.transform.position;
            foreach (GameObject tracked in TrackedObject)
            {
                if (Vector3.Distance(player_position, tracked.transform.position) < this.playerProximity)
                {
                    PlayerPrioritizedObjects.Add(tracked);
                }
            }
        }
    }

    void FixedUpdate()
    {
        if (prioritizeByPlayerDistance)
        {
            SetPlayerPriorityObjects();
            if (isColoringByPlayerProximity)
            {
                foreach (GameObject prioritised in this.PlayerPrioritizedObjects)
                {
                    prioritised.renderer.material.color = this.prioritizedColor;
                }
            }
        }
    }

    void OnSerializeNetworkView(BitStream stream, NetworkMessageInfo info)
    {
        if (isColoringPerSync)
        {
            syncedRoundColor = new Color(Random.value, Random.value, Random.value, 1.0f);
        }
        if (isShowingDebug)
        {
            Debug.Log("Last sync time: " + timeLastHighPrioritySync);
            Debug.Log("Current time: " + Time.time);
        }

        if (Time.time - timeLastHighPrioritySync > timeBetweenSyncHighPriority)
        {
            Debug.Log("Syncing set.");
            timeLastHighPrioritySync = Time.time;
            foreach (GameObject obj in TrackedObject)
            {
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
