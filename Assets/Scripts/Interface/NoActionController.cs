using UnityEngine;
using VRBrush.Interface;

namespace VRBrush.Interface
{
    /// <summary>
    /// Free Move controller - allows free movement in VR environment
    /// Activates XR locomotion system when selected
    /// Does not perform any brush operations
    /// </summary>
    public class NoActionController : MonoBehaviour, IInterfaceController
    {
        public string ControllerName => "Free Move";
        
        public void SetInputSource(GameObject inputSource) 
        { 
            // NoActionController doesn't need input source handling
            // Locomotion activation is managed by BrushUIManager
        }
    }
}