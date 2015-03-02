using UnityEngine;
using System.Collections.Generic;

public enum SyncHandler { snap, simplesmoothing, firstorder, secondorder, adaptivehigherorder };

public struct DeftRigidBodyState
{
  public double timestamp;
  public Vector3 pos;
  public Vector3 velocity;
  public Quaternion rot;
  public Vector3 angularVelocity;
}

public class RigidBodyManager : MonoBehaviour
{

  public Queue<GameObject> objectsToSync;
  public Dictionary<GameObject, DeftRigidBodyState> objecToState;
  public HashSet<GameObject> allTrackedObjects;

  // syncs in order of priority
  // priority suggests how many to sync per syncing set
  public Dictionary<int, HashSet<GameObject>> objectsByPriority;

  public int trackingLayer;
  public bool isShowingDebug;
  public bool isShowingLatency;

  public int playerPriority; // between 1 and 10
  // indicates number of player prioritised items to sync
  // before queue is rebuilt

  public SyncHandler syncHandler;

  public bool prioritizeByPlayerDistance;
  public float playerPriorityProximity;
  public float playerDontSyncProximity;

  public float timeBetweenSyncHighPriority = 0.1f;
  public float timeBetweenSyncLowPriority = 1.0f;
  public float timeMaximumUnsynced = 5.0f;

  float timeLastHighPrioritySync;
  float timeLastLowPrioritySync;
  float timeLastAllSynced;

  public bool useVisualizer;


  void Start()
  {
    objectsToSync = new Queue<GameObject>();
    allTrackedObjects = new HashSet<GameObject>();
    objectsByPriority = new Dictionary<int, HashSet<GameObject>>();
    for (int i = 1; i < 11; i++)
    {
      objectsByPriority[i] = new HashSet<GameObject>();
    }
  }

  public void ResetTrackedObjects()
  {
    allTrackedObjects.Clear();
    for (int i = 1; i < 11; i++)
    {
      objectsByPriority[i] = new HashSet<GameObject>();
    }
    foreach (GameObject obj in FindObjectsOfType<GameObject>())
    {
      if (obj.layer == trackingLayer)
      {
        allTrackedObjects.Add(obj);
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

    // direct from a paper
    // maybe not be best but don't mess with this please
    // -- calben
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
    foreach (GameObject player in GameObject.FindGameObjectsWithTag("Player"))
    {
      Vector3 player_position = player.transform.position;
      foreach (GameObject tracked in objectsToSync)
      {
        double distance = Vector3.Distance(player_position, tracked.transform.position);
        // this strcture can reduce passes
        // preferably don't modify this
        if (distance < this.playerDontSyncProximity)
        {
          continue;
        }
        else if (distance < this.playerPriorityProximity)
        {
          objectsByPriority[this.playerPriority].Add(tracked);
        }
        else
        {
          continue;
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
    }
  }

  void OnSerializeNetworkView(BitStream stream, NetworkMessageInfo info)
  {
    if(this.useVisualizer)
    {
      // note using the visualizer will probably hugely slow down your simulation
      this.gameObject.GetComponent<RigidBodyManagerVisualizer>().ShowVisualization();
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
