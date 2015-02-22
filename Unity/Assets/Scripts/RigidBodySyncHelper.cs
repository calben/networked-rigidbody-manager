using UnityEngine;
using System.Collections;

public class RigidBodySyncHelper : MonoBehaviour
{

    float lastSync;
    public float syncRate;
    public SyncHandler syncHandler = SyncHandler.simplesmoothing;
    public RigidBodyManager manager;

    void Start()
    {

    }

    void Update()
    {

    }

    void Awake()
    {
        foreach (NetworkView n in GetComponents<NetworkView>())
            n.observed = this;
    }

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
            case SyncHandler.simplesmoothing:
                obj.transform.position = Vector3.Lerp(obj.transform.position, pos, 0.5f);
                obj.rigidbody.velocity = Vector3.Lerp(obj.rigidbody.velocity, velocity, 0.5f);
                obj.rigidbody.rotation = Quaternion.Slerp(obj.rigidbody.rotation, rot, 0.5f);
                obj.rigidbody.angularVelocity = Vector3.Lerp(obj.rigidbody.angularVelocity, angular_velocity, 0.5f);
                break;
        }
        if (manager.isColoringPerSync)
        {
            obj.renderer.material.color = manager.syncedRoundColor;
        }
    }

    void OnSerializeNetworkView(BitStream stream, NetworkMessageInfo info)
    {
        if (stream.isWriting)
        {
            pos = this.rigidbody.position;
            rot = this.rigidbody.rotation;
            velocity = this.rigidbody.velocity;
            angular_velocity = this.rigidbody.angularVelocity;

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
            SyncObject(this.gameObject);
        }
    }
}
