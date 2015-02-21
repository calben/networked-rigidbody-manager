﻿using UnityEngine;
using System.Collections;

public class DeftNetwork : MonoBehaviour
{
  private const string typeName = "DeftNetwork";
  private const string gameName = "DeftRoom";

  private bool refreshHosts = false;
  private bool isJoining = false;
  private bool isHosting = false;
  private HostData[] hostList;

  void Start()
  {
    Application.runInBackground = true;
  }

  void OnGUI()
  {
    if (!Network.isClient && !Network.isServer)
      if (isJoining)
      {
        if (hostList != null)
          for (int i = 0; i < hostList.Length; i++)
            if (GUI.Button(new Rect(400, 100 + (110 * i), 300, 100), hostList[i].gameName))
              JoinServer(hostList[i]);
      }
      else if (isHosting)
      {
        HostServer();
      }
      else
      {
        if (GUI.Button(new Rect(100, 100, 300, 100), "Start Server"))
          isHosting = true;
        if (GUI.Button(new Rect(100, 250, 300, 100), "Refresh Hosts to Join"))
        {
          isJoining = true;
          RefreshHostList();
        }
      }
  }

  private void HostServer()
  {
    Network.InitializeServer(16, 25000, false);
    MasterServer.RegisterHost(typeName, gameName);
  }

  void OnServerInitialized()
  {
  }

  void FixedUpdate()
  {
    if (refreshHosts && MasterServer.PollHostList().Length > 0)
    {
      refreshHosts = false;
      hostList = MasterServer.PollHostList();
    }
  }

  private void RefreshHostList()
  {
    if (!refreshHosts)
    {
      refreshHosts = true;
      MasterServer.RequestHostList(typeName);
    }
  }

  private void JoinServer(HostData hostData)
  {
    Network.Connect(hostData);
  }

  void OnConnectedToServer()
  {
      GameObject.Find("MapMaker").GetComponent<MakeMap>().PlaceCubes();
  }

}
