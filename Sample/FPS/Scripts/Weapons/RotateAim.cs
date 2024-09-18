using UnityEngine;
using System.Collections;
using System.Diagnostics;

/*
 * Rotates the spine of the agent such that the gun points in the direction of the target.
 * */

namespace ArcaneOnyx.BehaviorTree.Sample
{
    public class RotateToAimGunScript : MonoBehaviour
    {
        public Vector3 rotationOffset;
        public Transform spineBone;
        public Transform bulletSpawnTransform;
        //[HideInInspector]
        public Transform targetTransform;

        public bool shouldDebug = false;

        public Vector3 maximumRotationAngles = new Vector3(90, 90, 90);

        public float rotationSpeed = 5;

        Quaternion spineRotationLastFrame;
        Vector3 tempSpineLocalEulerAngles;

        public bool isEnabled = false;

        public float minDistToAim = 0f;

        Quaternion targetRot;

        public int maxIterations = 2;
        public float minAngle = 5f;

        public bool useHighQualityAiming = false;
        public bool stopForCover = false;

        void Awake()
        {
            if (spineBone)
                spineRotationLastFrame = spineBone.rotation;
            else
            {
                this.enabled = false;
            }
        }

        void Start()
        {
            //if(ControllerScript.pMode)
            {
                //minDistToAim = 10f;
            }
        }

        void LateUpdate()
        {
            if (isEnabled && targetTransform && minDistToAim < Vector3.Distance(spineBone.position, targetTransform.position) && !stopForCover /*&& !Physics.Linecast(bulletSpawnTransform.position, targetTransform.position, TacticalAI.ControllerScript.currentController.layerMask)*/)
            {

                /*
                if (!useHighQualityAiming || ControllerScript.pMode)
                {
                    //Rotate the spine bone so the gun (roughly) aims at the target
                    spineBone.rotation = Quaternion.FromToRotation(bulletSpawnTransform.forward, targetTransform.position - bulletSpawnTransform.position) * spineBone.rotation;
                    tempSpineLocalEulerAngles = spineBone.localEulerAngles;


                    //Stop our agent from breaking their back by rotating too far
                    tempSpineLocalEulerAngles = new Vector3(ResetIfTooHigh(tempSpineLocalEulerAngles.x, maximumRotationAngles.x),
                                                            ResetIfTooHigh(tempSpineLocalEulerAngles.y, maximumRotationAngles.y),
                                                            ResetIfTooHigh(tempSpineLocalEulerAngles.z, maximumRotationAngles.z));

                    spineBone.localEulerAngles = tempSpineLocalEulerAngles;
                    targetRot = spineBone.rotation;

                    //Smoothly rotate to the new position.  
                    spineBone.rotation = Quaternion.Slerp(spineRotationLastFrame, targetRot, Time.deltaTime * rotationSpeed);
                    spineRotationLastFrame = spineBone.rotation;
                }*/
                //else
                {
                    //NEW CODE!
                    float distDelta = 129601;
                    int itsNow = maxIterations;

                    while (itsNow > 0 && distDelta > minAngle)
                    {
                        float rayDistance;
                        Vector3 origin = bulletSpawnTransform.TransformPoint(bulletSpawnTransform.position);
                        Plane plane = new Plane(spineBone.forward, spineBone.position);

                        if (plane.Raycast(new Ray(bulletSpawnTransform.position, -bulletSpawnTransform.forward), out rayDistance))
                        {
                            origin = bulletSpawnTransform.position + bulletSpawnTransform.forward * rayDistance;
                        }

                        Quaternion rotationIteration = Quaternion.FromToRotation(bulletSpawnTransform.forward, targetTransform.position - origin);
                        distDelta = rotationIteration.eulerAngles.sqrMagnitude;
                        itsNow--;

                        spineBone.rotation = rotationIteration * spineBone.rotation;
                    }

                    spineBone.rotation = Quaternion.Slerp(spineRotationLastFrame, spineBone.rotation, Time.deltaTime * rotationSpeed);
                    spineRotationLastFrame = spineBone.rotation;
                }

                if (shouldDebug)
                {
                    UnityEngine.Debug.DrawRay(bulletSpawnTransform.position, bulletSpawnTransform.forward * 1000, Color.red);
                    UnityEngine.Debug.DrawLine(bulletSpawnTransform.position, targetTransform.position, Color.blue);
                }
            }
            else
            {
                //Smoothly return to the default position if we're not engaged to a target.  More or less mirrors the agent's animations.
                targetRot = spineBone.rotation;
                spineBone.rotation = Quaternion.Slerp(spineRotationLastFrame, targetRot, Time.deltaTime * rotationSpeed*2);
                spineRotationLastFrame = spineBone.rotation;
            }
        }

        public void Activate()
        {
            /* 
            if (shouldDebug)
            {
                StackTrace stackTrace = new StackTrace();
                UnityEngine.Debug.Log(stackTrace.GetFrame(1).GetMethod().Name);
            }*/

            spineRotationLastFrame = spineBone.rotation;
            isEnabled = true;
        }

        public void Deactivate()
        {
            isEnabled = false;
        }

        public void SetTargetTransform(Transform x)
        {
            targetTransform = x;
        }

        //Can't really decide which of the following two methods to use.
        float ClampEulerAngles(float r, float lim)
        {
            if (r > 180)
                r -= 360;

            r = Mathf.Clamp(r, -lim, lim);

            return r;
        }


        float ResetIfTooHigh(float r, float lim)
        {
            
            if (r > 180)
                r -= 360;

            if (r < -lim || r > lim)
            {
                return 0;
            }
            else
                return r;
        }
    }
}
