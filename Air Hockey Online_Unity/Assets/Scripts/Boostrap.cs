using MH.Scripts;
using UnityEngine;

namespace MH.GameLogic
{
    public class Bootstrap : MonoBehaviour
    {
        private NetworkClient _networkClient;

        void Start()
        {
            _networkClient = new NetworkClient();
            _networkClient.Init();

            Application.targetFrameRate = 60;   
        }

        void Update()
        {
            // LiteNetLib requires PollEvents every frame to process connection/receive events
            _networkClient?.PollEvents();

            if ( Input.GetMouseButtonDown(0))
            {
                TestSendPacket();   
            }
        }

        void TestSendPacket()
        {
            var mousePos = Input.mousePosition;
            var packet = new c2s_mouse_pos
            {
                X = mousePos.x,
                Y = mousePos.y
            };
            _networkClient?.Send(packet);   

        }
    }
}
