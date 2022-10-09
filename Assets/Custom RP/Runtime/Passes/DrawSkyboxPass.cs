using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine.Rendering.Custom
{
    public class DrawSkyboxPass : ScriptableRenderPass
    {
        public DrawSkyboxPass(RenderPassEvent evt)
        {
            base.profilingSampler = new ProfilingSampler(nameof(DrawSkyboxPass));
            renderPassEvent = evt;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            context.DrawSkybox(renderingData.cameraData.camera);
        }
    }
}

