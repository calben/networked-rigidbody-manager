using UnityEngine;
using System.Collections.Generic;

public enum SyncHandler { snap, simplesmoothing, firstorder, secondorder, adaptivehigherorder };

public struct DeftRigidBodyState
{
  internal double timestamp;
  internal Vector3 pos;
  internal Vector3 velocity;
  internal Quaternion rot;
  internal Vector3 angularVelocity;
}

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
  public bool syncThroughManager;
  public SyncHandler syncHandler;

  public bool prioritizeByPlayerDistance;
  public float playerPriorityProximity;
  public float playerDontSyncProximity; // YOU NEED THIS FOR PROPER RIGIDBODY FUNCTION

  public float timeBetweenSyncHighPriority = 0.1f;
  public float timeBetweenSyncLowPriority = 1.0f;
  public float timeMaximumUnsynced = 5.0f;

  float timeLastHighPrioritySync;
  float timeLastLowPrioritySync;
  float timeLastAllSynced;
  public Color syncedRoundColor;
  public Color prioritizedColor = Color.yellow;


  void Start()
  {
    objectsToSync = new HashSet<GameObject>();
    allTrackedObjects = new HashSet<GameObject>();
    objectsToPrioritySync = new HashSet<GameObject>();
  }

  public void ResetTrackedObjects()
  {
    objectsToSync.Clear();
    allTrackedObjects.Clear();
    objectsToPrioritySync.Clear();
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
        // sync objects
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
        SnapSync(obj);
        break;
      case SyncHandler.simplesmoothing:
        FirstOrderSync(obj);
        break;
      case SyncHandler.firstorder:
        FirstOrderSync(obj);
        break;
      case SyncHandler.secondorder:
        //SecondOrderSync(obj);
        break;
    }
    if (isColoringPerSync)
    {
      obj.renderer.material.color = syncedRoundColor;
    }
  }

  void SnapSync(GameObject obj)
  {
    obj.transform.position = pos;
    obj.rigidbody.velocity = velocity;
    obj.rigidbody.rotation = rot;
    obj.rigidbody.angularVelocity = angular_velocity;
  }

  void FirstOrderSync(GameObject obj)
  {
    obj.transform.position = Vector3.Lerp(obj.transform.position, pos, 0.5f);
    obj.rigidbody.velocity = Vector3.Lerp(obj.rigidbody.velocity, velocity, 0.5f);
    obj.rigidbody.rotation = Quaternion.Slerp(obj.rigidbody.rotation, rot, 0.5f);
    obj.rigidbody.angularVelocity = Vector3.Lerp(obj.rigidbody.angularVelocity, angular_velocity, 0.5f);
  }

  void PHBRSync(DeftRigidBodyState[] states)
  {
    // assuming states in correct order
    // assuming timesteps approximately equal
    // assuming order with 0..n being most recent to least recent
    while (states[0].timestamp < Time.time)
    {
      float d1 = (float)(states[0].timestamp - states[1].timestamp);
      float d2 = (float)(states[1].timestamp - states[2].timestamp);
      DeftRigidBodyState update = new DeftRigidBodyState();
      float tmp1 = 2 * d1 * d1 / d2 / (d1 + d2);
      float tmp2 = 2 * d1 / d2 + 1;
      float tmp3 = 2 * d1 / (d1 + d2) + 1;
      update.pos = tmp1 * states[3].pos - tmp2 * states[1].pos + tmp3 * states[0].pos;
      update.velocity = 1 / (2 * d1) * states[1].pos - 2 / d1 * states[0].pos + 3 / (2 * d1) * update.pos;
      update.timestamp = states[0].timestamp + d1;
      for (int i = states.Length - 1; i >= 1; i--)
      {
        states[i] = states[i - 1];
      }
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
        }
        else if (Vector3.Distance(player_position, tracked.transform.position) < this.playerPriorityProximity)
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

  void SetSyncRateForNetworkViews()
  {
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
    if (!syncThroughManager)
    {

    }
  }

  void OnSerializeNetworkView(BitStream stream, NetworkMessageInfo info)
  {
    if (syncThroughManager)
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
    else
    {

    }
  }
}
