using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityModManagerNet;

namespace DayNightMod
{
    public class Main
    {
        // altitude = x, azimuth = y
        static float culminationAltitude = 72f;
        static float nadirAltitude = -72f;
        static float sunriseAzimuth = -87f;
        static float sunsetAzimuth = 87f;
        static float dayIntensity = 2.4f;
        static float nightIntensity = 0f;

        // sun defaults
        static float defaultAltitude = 37.04399f;
        static float defaultAzimuth = 68.8f;
        static float defaultIntensity = 2.4f;

        // TODO: https://sunrise-sunset.org/api
        static float sunriseTime = 21600f;
        static float sunsetTime = 64800f;
        static float dawnTime = sunriseTime - 2100f;
        static float duskTime = sunsetTime + 2100f;
        const float secondsInDay = 86400f;

        static Light sun = null;

        static bool Load(UnityModManager.ModEntry modEntry)
		{
            modEntry.OnToggle = OnToggle;
            return true;
        }

        static bool OnToggle(UnityModManager.ModEntry modEntry, bool isTogglingOn)
		{
            if (isTogglingOn)
            {
                modEntry.OnFixedUpdate = OnFixedUpdate;
                return true;
			}

            modEntry.OnFixedUpdate = null;
            UpdateSun(defaultAltitude, defaultAzimuth, defaultIntensity);

            return true;
        }

        static void OnFixedUpdate(UnityModManager.ModEntry modEntry, float delta)
		{
            if (!FindSun())  return;

            DateTime now = DateTime.Now;
            TimeSpan timeSpanSinceMidnight = now.Subtract(new DateTime(now.Year, now.Month, now.Day));
            float secondsSinceMidnight = (float)timeSpanSinceMidnight.TotalSeconds;

            float altitude = (nadirAltitude - culminationAltitude) / 2f
                * Mathf.Cos(2f * Mathf.PI * secondsSinceMidnight / secondsInDay)
                + (culminationAltitude - nadirAltitude) / 2f;
            float azimuth = (sunriseAzimuth - sunsetAzimuth) / 2f
                * Mathf.Sin(2f * Mathf.PI * secondsSinceMidnight / secondsInDay)
                + (sunsetAzimuth + sunriseAzimuth) / 2f;
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
    }
}
