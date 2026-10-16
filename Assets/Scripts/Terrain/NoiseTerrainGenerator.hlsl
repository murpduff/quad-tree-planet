float LayeredNoise(float3 pos, int octaves, float frequency, float amplitude){ //on flat sphere geometry
    float h = 0;
    float f = frequency;
    float a = 1;

    for(int i = 0; i < octaves; i++){
        h += SimplexNoise3D(pos * f) * a;
        f *= 2;
        a /= 2;
    }

    return h * amplitude;
}

float3 GetNoiseNormal(float3 pos, float3 sphereNormal, int octaves, float frequency, float amplitude){ //all on flat sphere geometry
    float3 up = abs(sphereNormal.y) < 0.999 ? float3(0.0, 1.0, 0.0) : float3(1.0, 0.0, 0.0);

    float3 tangent = normalize(cross(up, sphereNormal));
    float3 bitangent = cross(sphereNormal, tangent);

    float eps = 0.001;
    float dx = (LayeredNoise(pos + bitangent * eps, octaves, frequency, amplitude) - LayeredNoise(pos, octaves, frequency, amplitude)) / (eps);
    float dy = (LayeredNoise(pos + tangent * eps, octaves, frequency, amplitude) - LayeredNoise(pos, octaves, frequency, amplitude)) / (eps);

    return normalize(float3(-dx, 1.0, -dy));
}

