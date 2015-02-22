using UnityEngine;
using System.Collections.Generic;

public enum SyncHandler { snap, simplesmoothing };

public class RigidBodyManager : MonoBehaviour
{

    public HashSet<GameObject> allTrackedObjects;
    public HashSet<GameObject> objectsToSync;
    public HashSet<GameObject> objectsToPrioritySync;

    public int layer;
    public bool isShowingDebug;
    public bool isShowingLatency;
    public bool isColoringPerSync;
    public bool isColoringByPlayerProximity;
    public SyncHandler syncHandler;

    public bool prioritizeByPlayerDistance;
    public float playerPriorityProximity;
    public float playerDontSyncProximity; // YOU NEED THIS FOR PROPERTY RIGIDBODY FUNCTION

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

        objectsToSync = new HashSet<GameObject>();
        allTrackedObjects = new HashSet<GameObject>();
        objectsToPrioritySync = new HashSet<GameObject>();
        foreach (GameObject obj in FindObjectsOfType<GameObject>())
        {
            if (obj.layer == layer)
            {
                allTrackedObjects.Add(obj);
                if (isColoringPerSync)
                {
                    obj.renderer.material.color = Color.green;
                }
            }
        }
        if (isShowingDebug)
            Debug.Log("Tracking " + allTrackedObjects.Count + " objects for automated networking.");
    }

    [RPC]
    public void ShareTrackedObjectList()
    {
        if (Network.isServer)
        {
            this.ResetTrackedObjects();
            foreach (GameObject obj in this.objectsToSync)
            {

            }
        }
        if (Network.isClient)
        {
            this.objectsToSync.Clear();

        }
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
        if (isShowingDebug)
        {
            Debug.Log("Syncing object: " + obj.GetInstanceID());
        }
        switch (syncHandler)
        {
            case SyncHandler.snap:
                obj.transform.position = pos;
                obj.rigidbody.velocity = velocity;
                obj.rigidbody.rotation = rot;
                obj.rigidbody.angularVelocity = angular_velocity;
                break;
            case SyncHandler.simplesmoothing:
                obj.transform.position = Vector3.Lerp(obj.transform.position, pos, 0.5f);
                obj.rigidbody.velocity = Vector3.Lerp(obj.rigidbody.velocity, velocity, 0.5f);
                obj.rigidbody.rotation = Quaternion.Slerp(obj.rigidbody.rotation, rot, 0.5f);
                obj.rigidbody.angularVelocity = Vector3.Lerp(obj.rigidbody.angularVelocity, angular_velocity, 0.5f);
                break;
        }
        if (isColoringPerSync)
        {
            obj.renderer.material.color = syncedRoundColor;
        }
    }

    void SetPlayerPriorityObjects()
    {
        objectsToPrioritySync.Clear();
        foreach (GameObject player in GameObject.FindGameObjectsWithTag("Player"))
        {
            Vector3 player_position = player.transform.position;
            foreach (GameObject tracked in objectsToSync)
            {
                if (Vector3.Distance(player_position, tracked.transform.position) < this.playerPriorityProximity)
                {
                    objectsToSync.Remove(tracked);
                } else if (Vector3.Distance(player_position, tracked.transform.position) < this.playerPriorityProximity)
                {
                    objectsToPrioritySync.Add(tracked);
                }
                else
                {
                    objectsToSync.Add(tracked);
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
                foreach (GameObject prioritised in this.objectsToPrioritySync)
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
            Debug.Log("Last low priority sync time: " + timeLastLowPrioritySync);
            Debug.Log("Current time: " + Time.time);
        }

        if (Time.time - timeLastLowPrioritySync > timeBetweenSyncHighPriority)
        {
            Debug.Log("Syncing set.");
            timeLastHighPrioritySync = Time.time;
            foreach (GameObject obj in objectsToSync)
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
