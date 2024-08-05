using System;
using UnityEngine;
using ARSandbox;

namespace Sandbox.Scripts.ServerClient
{
    public class SandboxClient: MonoBehaviour
    {
        public ARSandbox.Sandbox Sandbox;
        
        private void Setup()
        {
            Debug.Log("Server Sandbox Setup");
        }

        private void OnEnable()
        {
            Debug.Log("Server Sandbox Enabled");
        }

        private void Update()
        {
            
        }
        
    }
}