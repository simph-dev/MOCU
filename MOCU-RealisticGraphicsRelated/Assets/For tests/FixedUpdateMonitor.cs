using UnityEngine;
using System.Collections.Generic;
using System.Diagnostics;


public class FixedUpdateMonitor : MonoBehaviour
{
    private Stopwatch stopwatch;
    private long elapsedMs;
    private long tickFrequency;
    private int frameCount = 0;
    private const int logFrequency = 100;

    private double[] array;

    void Start()
    {
        stopwatch = Stopwatch.StartNew();
        Time.fixedDeltaTime = 0.001f; // 1000 Гц (1 мс на цикл физики)
        array = new double[logFrequency];
        elapsedMs = stopwatch.ElapsedTicks;
        tickFrequency = Stopwatch.Frequency;
    }

    void FixedUpdate()
    {
        var em = stopwatch.ElapsedTicks;
        UnityEngine.Debug.Log($"in 1 sec: {tickFrequency} ticks. passed: {em - elapsedMs}");
        elapsedMs = em;
        /*currentTime = Time.realtimeSinceStartup;
        deltaTime = currentTime - lastFixedUpdateTime;
        lastFixedUpdateTime = currentTime;

        //array[frameCount % logFrequency] = deltaTime;

        if (Time.realtimeSinceStartup - lastLogTime >= 1f) // Каждую секунду
        {
            Debug.Log($"FixedUpdate вызван {frameCount} раз за последнюю секунду");
            frameCount = 0;
            lastLogTime = Time.realtimeSinceStartup;
        }

        *//*if (frameCount % logFrequency == 0)
            Debug.Log(string.Join(", ", array));*//*

        frameCount++;*/
    }
}