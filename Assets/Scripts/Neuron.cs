using System;

using UnityEngine;

using Random = UnityEngine.Random;

public class Neuron : MonoBehaviour
{
    public int index { get; protected set; }
    public int expected { get; set; }
    public double signal { get; protected set; }
    public double result { get; protected set; }
    public double[] weights { get; protected set; }

    public Neuron[] next;
    
    public Neuron[] prevu;
    
    public Neuron()
    {
        signal = 0;

        weights = null;

        next = null;
        prevu = null;
    }
    
    public Neuron(int index)
    {
        signal = 0;
        this.index = index;

        next = null;
        prevu = null;
    }
    
    public void Input_Signal(double signal)
    {
        this.signal = signal;
    }
    
    public void Weighing()
    {
        weights = new double[prevu.Length];
        
        for (int i = 0; i < prevu.Length; i++)
        {
            if (Random.Range(0, 2) == 1) weights[i] = Random.Range(0.0f, 1.0f);
            else weights[i] = Random.Range(0.0f, 1.0f) * (-1);
        }
    }

    public void Add_Weights(double[] weights)
    {
        this.weights = new double[prevu.Length];
        
        for (int i = 0; i < this.weights.Length; i++)
            this.weights[i] = weights[i];
    }
    
    public void Start()
    {
        signal = 0;
        result = 0;
        
        Signal_Calculation();
        Processing();
    }
    
    void Signal_Calculation()
    {
        if(this.prevu[0].prevu == null)
        {
            for (int i = 0; i < prevu.Length; i++)
            {
                signal += prevu[i].signal * weights[i];
            }
        }
        
        else
        {
            for (int i = 0; i < prevu.Length; i++)
            {
                signal += prevu[i].result * weights[i];
            }
        }
    }
    
    void Processing()
    {
        result = 1 / (1 + Math.Pow(Math.E, -signal));
    }
}