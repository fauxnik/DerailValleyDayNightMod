using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityModManagerNet;
using VRTK;

namespace DayNightMod
{
    public class Main
    {
        // altitude = x, azimuth = y
        static float culminationAltitude = 72f;
        static float nadirAltitude = -72f;
        // TODO: make use of these
        static float sunriseAzimuth = -87f;
        static float sunsetAzimuth = 87f;
        static float dayIntensity = 2.4f;
        static float nightIntensity = dayIntensity * 0.014f;

        // sun defaults
        static float defaultAltitude = 37.04399f;
        static float defaultAzimuth = 68.8f;
        static float defaultIntensity = 2.4f;

        // skybox defaults
        static float defaultRotation = 113f;
        static float defaultExposure = 1f;

        // ambient defaults
        static UnityEngine.Rendering.AmbientMode defaultAmbientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        static Color defaultAmbientLight = Color.white;
        static Color defaultFog = Color.cyan;

        // TODO: https://sunrise-sunset.org/api
        static float sunriseTime = 21600f;
        static float sunsetTime = 64800f;
        static float dawnTime = sunriseTime - 2100f;
        static float duskTime = sunsetTime + 2100f;
        const float secondsInDay = 86400f;

        static Light sun = null;
        static Light moon = null;
        //static AssetBundle assetBundle;
        static Texture[] skybox;

        static bool Load(UnityModManager.ModEntry modEntry)
		{
            // TODO: figure out how to use an asset bundle
            //string assetBundlePath = modEntry.Path + "Resources\\skybox";
            //assetBundle = AssetBundle.LoadFromFile(assetBundlePath);
            //if (assetBundle == null)
            //    modEntry.Logger.Error("failed to load skybox asset bundle [" + assetBundlePath + "]");
            //else
            //    modEntry.Logger.Log("loaded skybox asset bundle");

            skybox = new Texture[6];
            var sides = new string[] { "Front", "Back", "Left", "Right", "Top", "Bottom" };
            for (int i = 0; i < sides.Length; i++)
			{
                string side = sides[i];
                string filePath = modEntry.Path + "Resources\\Dusk_" + side + ".png";
                byte[] fileData = File.ReadAllBytes(filePath);
                
                if (fileData == null)
				{
                    modEntry.Logger.Error(string.Format(
                        "failed to load skybox side ({0})",
                        filePath));
                    return false;
                }

                var texture = new Texture2D(1024, 1024);

                texture.LoadImage(fileData);
                texture.Apply(true, true);

                skybox[i] = texture;
            }

            modEntry.OnToggle = OnToggle;

            return true;
        }

        static bool OnToggle(UnityModManager.ModEntry modEntry, bool isTogglingOn)
		{
            if (isTogglingOn)
            {
                modEntry.OnUpdate = OnUpdate;
                return true;
			}

            modEntry.OnUpdate = null;
            UpdateLight(sun, defaultAltitude, defaultAzimuth, defaultIntensity);
            UpdateSkybox(defaultRotation, defaultExposure, 1f);
            RenderSettings.ambientMode = defaultAmbientMode;

            return true;
        }

        static void OnUpdate(UnityModManager.ModEntry modEntry, float delta)
		{
            if (!FindSun())  return;

            DateTime now = DateTime.Now;
            TimeSpan timeSpanSinceMidnight = now.Subtract(new DateTime(now.Year, now.Month, now.Day));
            float secondsSinceMidnight = 0f; // 43200f; // (float)timeSpanSinceMidnight.TotalSeconds;

            float sunAltitude = (nadirAltitude - culminationAltitude) / 2f
                * Mathf.Cos(2f * Mathf.PI * secondsSinceMidnight / secondsInDay)
                + (culminationAltitude + nadirAltitude) / 2f;
            float sunAzimuth = 360f * secondsSinceMidnight / secondsInDay - 180f;
            float sunIntensity = dayIntensity;
            if (secondsSinceMidnight < dawnTime || secondsSinceMidnight > duskTime)
			{
                sunIntensity = 0f;
			}
            else if (secondsSinceMidnight < sunriseTime)
			{
                sunIntensity = (dayIntensity)
                    * (secondsSinceMidnight - dawnTime)
                    / (sunriseTime - dawnTime);
			}
            else if (secondsSinceMidnight > sunsetTime)
			{
                sunIntensity = (dayIntensity)
                    * (duskTime - secondsSinceMidnight)
                    / (duskTime - sunsetTime);
			}
            UpdateLight(sun, sunAltitude, sunAzimuth, sunIntensity);

            float moonAltitude = (nadirAltitude - culminationAltitude) / 2f
                * Mathf.Cos(2f * Mathf.PI * secondsSinceMidnight / secondsInDay + Mathf.PI)
                + (culminationAltitude + nadirAltitude) / 2f;
            float moonAzimuth = 360f * secondsSinceMidnight / secondsInDay;
            float moonIntensity = 0f;
            if (secondsSinceMidnight < dawnTime || secondsSinceMidnight > duskTime)
            {
                moonIntensity = nightIntensity;
            }
            else if (secondsSinceMidnight < sunriseTime)
            {
                moonIntensity = (nightIntensity)
                    * (dawnTime - secondsSinceMidnight)
                    / (dawnTime - sunriseTime);
            }
            else if (secondsSinceMidnight > sunsetTime)
            {
                moonIntensity = (nightIntensity)
                    * (secondsSinceMidnight - duskTime)
                    / (sunsetTime - duskTime);
            }
            UpdateLight(moon, moonAltitude, moonAzimuth, moonIntensity);

            // this affects the VR loading environment, not the game environment!
            //SteamVR_Skybox.SetOverride(skybox[0], skybox[1], skybox[2], skybox[3], skybox[4], skybox[5]);
            // TODO: can the skybox texture be replaced at night?

            float skyRotation = sunAzimuth + 180f % 360f;
            float skyExposure =  sunIntensity * (dayIntensity - 0.05f) / Mathf.Pow(dayIntensity, 2) + 0.05f;
            float ambientIntensity = sunIntensity * (dayIntensity - 0.05f) / Mathf.Pow(dayIntensity, 2) + 0.05f;
            UpdateSkybox(skyRotation, skyExposure, ambientIntensity);

            Debug.Log(string.Format("[Sun Settings]\n" +
                "  >sun altitude={0}\n" +
                "  >sun azimuth={1}\n" +
                "  >sun intensity={2}\n" +
                "  >sky rotation={3}\n" +
                "  >sky exposure={4}\n" +
                "  >ambient={5}",
                sunAltitude,
                sunAzimuth,
                sunIntensity,
                skyRotation,
                skyExposure,
                ambientIntensity));

            // TODO: remove this when re-enbling time-based lighting
            modEntry.OnUpdate = null;
        }

        static void UpdateLight(Light light, float altitude, float azimuth, float intensity)
		{
            if (light == null) return;

            Vector3 angles = sun.transform.rotation.eulerAngles;
            angles.x = altitude;
            angles.y = azimuth;
            light.transform.rotation = Quaternion.Euler(angles);
            light.intensity = intensity;
		}

        static void UpdateSkybox(float rotation, float exposure, float ambient)
		{
            RenderSettings.skybox.SetFloat("_Rotation", rotation);
            RenderSettings.skybox.SetFloat("_Exposure", exposure);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;

            float ambientRed = defaultAmbientLight.r * ambient;
            float ambientGreen = defaultAmbientLight.g * ambient;
            float ambientBlue = defaultAmbientLight.b * ambient;
            RenderSettings.ambientLight = new Color(ambientRed, ambientGreen, ambientBlue);

            float fogRed = defaultFog.r * ambient;
            float fogGreen = defaultFog.g * ambient;
            float fogBlue = defaultFog.b * ambient;
            RenderSettings.fogColor = new Color(fogRed, fogGreen, fogBlue);
        }

        static bool FindSun()
        {
            if (sun == null)
            {
                sun = UnityEngine.Object.FindObjectsOfType<Light>()
                    .FirstOrDefault((Light light) => light.type == LightType.Directional && light.name == "Directional Light");

                if (sun == null) return false;

                moon = UnityEngine.Object.Instantiate<Light>(sun);

                if (sun == moon)
                {
                    throw new Exception("sun and moon are the same!");
                }

                moon.color = new Color(moon.color.r * 0.7f, moon.color.g * 0.85f, moon.color.b);

                Vector3 sunDefault = sun.transform.rotation.eulerAngles;
                defaultAltitude = sunDefault.x;
                defaultAzimuth = sunDefault.y;
                defaultIntensity = sun.intensity;

                defaultRotation = RenderSettings.skybox.GetFloat("_Rotation");
                defaultExposure = RenderSettings.skybox.GetFloat("_Exposure");
                defaultAmbientMode = RenderSettings.ambientMode;
                defaultAmbientLight = RenderSettings.ambientLight;
                defaultFog = RenderSettings.fogColor;

                Debug.Log(string.Format("[DayNight] defaults:\n" +
                    "  >altitude={0}\n" +
                    "  >azimuth={1}\n" +
                    "  >intensity={2}\n" +
                    "  >rotation={3}\n" +
                    "  >exposure={4}\n" +
                    "  >ambient mode={5}\n" +
                    "  >ambient light={6}\n" +
                    "  >fog color={7}",
                    defaultAltitude,
                    defaultAzimuth,
                    defaultIntensity,
                    defaultRotation,
                    defaultExposure,
                    defaultAmbientMode,
                    defaultAmbientLight,
                    defaultFog));

                Debug.Log("skybox shader: " + RenderSettings.skybox.shader.name);
            }

            return true;
        }
    }
}
