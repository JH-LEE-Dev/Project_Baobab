using UnityEngine;

public static class NavParticleHelper
{
    public static void ApplyInstancedColor(ParticleSystem _particle, Color _color, ref bool _hasInstantiated)
    {
        if (true == _hasInstantiated || null == _particle)
        {
            return;
        }

        ParticleSystemRenderer _psr = _particle.GetComponent<ParticleSystemRenderer>();
        if (null != _psr && null != _psr.sharedMaterial)
        {
            Material _instancedMat = new Material(_psr.sharedMaterial);
            if (_instancedMat.HasProperty("_HDRColor"))
            {
                _instancedMat.SetColor("_HDRColor", _color);
            }
            else if (_instancedMat.HasProperty("_TintColor"))
            {
                _instancedMat.SetColor("_TintColor", _color);
            }
            else if (_instancedMat.HasProperty("_Color"))
            {
                _instancedMat.SetColor("_Color", _color);
            }
            _psr.material = _instancedMat;
            _hasInstantiated = true;
        }
    }
}
