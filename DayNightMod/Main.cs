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
        static float nightIntensity = 0f;

        // sun defaults
        static float defaultAltitude = 37.04399f;
        static float defaultAzimuth = 68.8f;
        static float defaultIntensity = 2.4f;

        // skybox defaults
        static float defaultRotation = 113f;
        static float defaultExposure = 1f;

        // color grading defaults
        static float defaultBrightness = 0f;

        // TODO: https://sunrise-sunset.org/api
        static float sunriseTime = 21600f;
        static float sunsetTime = 64800f;
        static float dawnTime = sunriseTime - 2100f;
        static float duskTime = sunsetTime + 2100f;
        const float secondsInDay = 86400f;

        static Light sun = null;
        static ColorGrading colorGrading = null;
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
            UpdateSun(defaultAltitude, defaultAzimuth, defaultIntensity);
            UpdateSkybox(defaultRotation, defaultExposure);
            colorGrading.brightness.value = defaultBrightness;

            return true;
        }

        static void OnUpdate(UnityModManager.ModEntry modEntry, float delta)
		{
            if (!FindSun())  return;
            if (!findColorGrading()) return;

            DateTime now = DateTime.Now;
            TimeSpan timeSpanSinceMidnight = now.Subtract(new DateTime(now.Year, now.Month, now.Day));
            float secondsSinceMidnight = 0f; // 43200f; // (float)timeSpanSinceMidnight.TotalSeconds;

            float altitude = (nadirAltitude - culminationAltitude) / 2f
                * Mathf.Cos(2f * Mathf.PI * secondsSinceMidnight / secondsInDay)
                + (culminationAltitude + nadirAltitude) / 2f;
            float azimuth = 360f * secondsSinceMidnight / secondsInDay - 180f;
            float intensity = dayIntensity;
            if (secondsSinceMidnight < dawnTime || secondsSinceMidnight > duskTime)
			{
                intensity = nightIntensity;
			}
            else if (secondsSinceMidnight < sunriseTime)
			{
                intensity = (dayIntensity - nightIntensity)
                    * (secondsSinceMidnight - dawnTime)
                    / (sunriseTime - dawnTime);
			}
            else if (secondsSinceMidnight > sunsetTime)
			{
                intensity = (dayIntensity - nightIntensity)
                    * (duskTime - secondsSinceMidnight)
                    / (duskTime - sunsetTime);
			}

            UpdateSun(altitude, azimuth, intensity);

            // this affects the VR loading environment, not the game environment!
            //SteamVR_Skybox.SetOverride(skybox[0], skybox[1], skybox[2], skybox[3], skybox[4], skybox[5]);
            // TODO: can the skybox texture be replaced at night?

            float rotation = azimuth + 180f % 360f;
            float ambientIntensity =  intensity * (dayIntensity - 0.16666f) / Mathf.Pow(dayIntensity, 2) + 0.16666f;
            UpdateSkybox(rotation, ambientIntensity);

            float brightness = intensity / dayIntensity - 1f;
            // none of these help to dim the scenery:
            //colorGrading.brightness.value = brightness;
            //colorGrading.postExposure.value = brightness;
            //colorGrading.masterCurve.value = new Spline(AnimationCurve.Linear(0, 0, 1, 0.16666f), 0, false, new Vector2(0, 1));

            Debug.Log(string.Format("[Sun Settings]\n" +
                "  >altitude={0}\n" +
                "  >azimuth={1}\n" +
                "  >intensity={2}\n" +
                "  >rotation={3}\n" +
                "  >ambient={4}\n" +
                "  brightness={5}",
                altitude,
                azimuth,
                intensity,
                rotation,
                ambientIntensity,
                brightness));
        }

        static bool findColorGrading()
		{
            if (colorGrading == null)
            {
                PostProcessVolume ppv = UnityEngine.Object.FindObjectOfType<PostProcessVolume>();
                if (ppv == null) return false;

                ppv.profile.TryGetSettings<ColorGrading>(out colorGrading);
                if (colorGrading == null) return false;

                defaultBrightness = colorGrading.brightness.value;
                Debug.Log("color grading brightness: " + colorGrading.brightness.value);
            }

            return true;
		}

        static bool FindSun()
		{
            if (sun == null)
            {
                sun = UnityEngine.Object.FindObjectsOfType<Light>()
                    .FirstOrDefault((Light light) => light.type == LightType.Directional && light.name == "Directional Light");

                if (sun == null) return false;

                Vector3 sunDefault = sun.transform.rotation.eulerAngles;
                defaultAltitude = sunDefault.x;
                defaultAzimuth = sunDefault.y;
                defaultIntensity = sun.intensity;

                Debug.Log(string.Format("[DayNight] defaults:\n" +
                    "  altitude={0}\n" +
                    "  azimuth={1}\n" +
                    "  intensity={2}",
                    defaultAltitude,
                    defaultAzimuth,
                    defaultIntensity));

                defaultRotation = RenderSettings.skybox.GetFloat("_Rotation");
                defaultExposure = RenderSettings.skybox.GetFloat("_Exposure");

                Debug.Log("skybox shader: " + RenderSettings.skybox.shader.name);
                Debug.Log("skybox rotation: " + defaultRotation);
                Debug.Log("skybox exposure: " + defaultExposure);
            }

            return true;
        }

        static void UpdateSun(float altitude, float azimuth, float intensity)
		{
            if (sun == null) return;

            Vector3 angles = sun.transform.rotation.eulerAngles;
            angles.x = altitude;
            angles.y = azimuth;
            sun.transform.rotation = Quaternion.Euler(angles);
            sun.intensity = intensity;
		}

        static void UpdateSkybox(float rotation, float exposure)
		{
            RenderSettings.skybox.SetFloat("_Rotation", rotation);
            RenderSettings.skybox.SetFloat("_Exposure", exposure);
        }
    }
}
