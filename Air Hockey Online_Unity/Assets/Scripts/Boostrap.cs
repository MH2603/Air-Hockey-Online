using UnityEngine;

namespace MH.Scripts
{
    public class Bootstrap : MonoBehaviour
    {
        private NetworkClient _networkClient;

        void Start()
        {
            _networkClient = new NetworkClient();
            _networkClient.Init();
        }

        void Update()
        {
            // LiteNetLib requires PollEvents every frame to process connection/receive events
            _networkClient?.PollEvents();
        }
    }
}
