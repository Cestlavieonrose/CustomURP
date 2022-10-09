using UnityEngine;

[DisallowMultipleComponent]
public class PerObjectMaterialProperties : MonoBehaviour
{
    static int baseColorId = Shader.PropertyToID("_BaseColor");
    static int cutoffId = Shader.PropertyToID("_Cutoff");
    static int metallicId = Shader.PropertyToID("_Metallic");
    static int smoothnessId = Shader.PropertyToID("_Smoothness");

    [SerializeField]
    Color baseColor = Color.white;
    [SerializeField, Range(0f, 1f)]
    float cutoff = 0.5f;
    static MaterialPropertyBlock block;

    [SerializeField, Range(0f, 1f)]
    float metallic = 0f;
    [SerializeField, Range(0f, 1f)]
    float smoothness = 0.5f;

    void OnValidate() {
        if (block == null)
        {
            block = new MaterialPropertyBlock();
        }

        //设置材质属性
        block.SetColor(baseColorId, baseColor);
        block.SetFloat(metallicId, metallic);
        block.SetFloat(smoothnessId, smoothness);
       // block.SetFloat(cutoffId, cutoff);

        GetComponent<Renderer>().SetPropertyBlock(block);
    }

    void Awake() {
        OnValidate();
    }
}
