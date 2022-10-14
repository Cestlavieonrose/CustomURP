using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ButtonClickFuncs : MonoBehaviour
{
    bool togglebtn = false;
    public void OnDisableKeyword()
    {
        togglebtn = !togglebtn;
        if (togglebtn)
            Shader.EnableKeyword("_ALPHATEST_ON");
        else
        {
            Shader.DisableKeyword("_ALPHATEST_ON");    
        }
        
    }
}
