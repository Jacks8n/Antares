using Antares.Graphics;
using Antares.Physics;
using Antares.SDF;
using UnityEngine;

namespace Antares.Scripts
{
    public class Test : MonoBehaviour
    {
        [SerializeField]
        private SDFScene _sdfScene;

        [SerializeField]
        private APhysicsScene _physicsScene;

        [SerializeField]
        private GameObject[] _emitterArray;

        private void OnEnable()
        {
            ARenderPipeline.AfterRenderPipelineLoaded += LoadObjects;
        }

        private void OnDisable()
        {
            ARenderPipeline.AfterRenderPipelineLoaded -= LoadObjects;
        }

        private void LoadObjects()
        {
            _sdfScene.enabled = true;
            _physicsScene.enabled = true;

            for (int i = 0; i < _emitterArray.Length; i++)
                _emitterArray[i].SetActive(true);
        }
    }
}
