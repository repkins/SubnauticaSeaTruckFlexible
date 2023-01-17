using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SubnauticaSeaTruckFlexible.FixedCamera
{
    internal class FixedCameraController : MonoBehaviour
    {
        private bool isActive = false;
        private bool toggleNextFrame = false;

        private Transform camTransform;
        private Transform origCamParentTransform;
        private Vector3 origCamLocalPosition;
        private Quaternion origCamLocalOrientation;

        private void Awake()
        {
            DevConsole.RegisterConsoleCommand(this, "fixedcam", false, false);
        }

        private void OnConsoleCommand_fixedcam()
        {
            this.toggleNextFrame = true;
        }

        private void LateUpdate()
        {
            if (this.toggleNextFrame)
            {
                FixedcamToggle();
                this.toggleNextFrame = false;
            }
        }

        private void FixedcamToggle()
        {
            this.isActive = !this.isActive;

            if (this.isActive)
            {
                this.camTransform = SNCameraRoot.main.transform;
                this.origCamParentTransform = this.camTransform.parent;

                this.origCamLocalPosition = this.camTransform.localPosition;
                this.origCamLocalOrientation = this.camTransform.localRotation;

                this.camTransform.SetParent(null, true);
            }
            else
            {
                this.camTransform.SetParent(this.origCamParentTransform, true);
                this.camTransform.localPosition = this.origCamLocalPosition; 
                this.camTransform.localRotation = this.origCamLocalOrientation;
            }
        }
    }
}
