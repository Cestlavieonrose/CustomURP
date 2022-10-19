using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using  UnityEngine.Rendering;

public class MeshBall : MonoBehaviour
{
    static int baseColorId = Shader.PropertyToID("_BaseColor");

    static int metallicId = Shader.PropertyToID("_Metallic");
    static int smoothnessId = Shader.PropertyToID("_Smoothness");

    [SerializeField]
    Mesh mesh = default;

    [SerializeField]
    Material material = default;

    Matrix4x4[] matrices = new Matrix4x4[1023];
    Vector4[] baseColors = new Vector4[1023];

    float[] metallics = new float[1023];
    float[] smoothnesss = new float[1023];

    MaterialPropertyBlock block;

    void Awake() {
        for (int i=0; i<matrices.Length; i++)
        {
            matrices[i] = Matrix4x4.TRS(Random.insideUnitSphere*10f, 
                                        Quaternion.Euler(Random.value*360f, Random.value*360f,Random.value*360f), 
                                        Vector3.one*Random.Range(0.5f, 1.5f));
            baseColors[i] = new Vector4(Random.value, Random.value, Random.value, Random.Range(0.5f, 1f));
            metallics[i] = Random.Range(0.0f, 1.0f)<0.25f ? 1f : 0f;
            smoothnesss[i] = Random.Range(0.05f, 0.95f);
        }
    }

    private void Update() {
        if (block == null)
        {
            block = new MaterialPropertyBlock();
            block.SetVectorArray(baseColorId, baseColors);
            block.SetFloatArray(metallicId, metallics);
            block.SetFloatArray(smoothnessId, smoothnesss);
            var position = new Vector3[1023];
            for (int i = 0; i < matrices.Length; i++)
            {
                position[i] = matrices[i].GetColumn(3);
            }
            var lightProbes = new SphericalHarmonicsL2[1023];
            LightProbes.CalculateInterpolatedLightAndOcclusionProbes(position, lightProbes, null);
            block.CopySHCoefficientArraysFrom(lightProbes);
        }
        
        Graphics.DrawMeshInstanced(mesh, 0,  material, matrices, 1023, block, UnityEngine.Rendering.ShadowCastingMode.On,
                            true, 0, null, UnityEngine.Rendering.LightProbeUsage.CustomProvided);

    }
}
