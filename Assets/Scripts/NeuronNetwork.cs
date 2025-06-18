using UnityEngine;

public class Neurons_Network: MonoBehaviour
{
    public Neuron[][] neurons { get; protected set; }

    public Neurons_Network(int input = 1, int hidden = 1, int output = 1, int layer = 1, double[][] weights = null)
    {
        //Debug.Log("??????? ?????? = " + weights?[120].Length);

        if (input <= 0) input = 1;
        if (hidden <= 0) hidden = 1;
        if (output <= 0) output = 1;
        if (layer <= 0) layer = 1;

        neurons = new Neuron[layer + 2][];

        for (int i = 0; i < neurons.Length; i++)
        {
            if (i == 0) neurons[i] = new Neuron[input];
            else if (i == neurons.Length - 1) neurons[i] = new Neuron[output];
            else neurons[i] = new Neuron[hidden];
        }

        Creat_neurons();

        if (weights == null) Weights_Created();
        else Weights_Add(weights);
    }

    protected void Creat_neurons()
    {
        for (int i = 0; i < neurons.Length; i++)
            for (int j = 0; j < neurons[i].Length; j++)
            {
                neurons[i][j] = new Neuron(j);
                
                if (i > 0) neurons[i][j].prevu = neurons[i - 1];
            }

        for (int i = neurons.Length - 2; i >= 0; i--)
            for (int j = 0; j < neurons[i].Length; j++)
                neurons[i][j].next = neurons[i + 1];
    }

    public void Weights_Created()
    {
        for (int i = 1; i < neurons.Length; i++)
            for (int j = 0; j < neurons[i].Length; j++)
                neurons[i][j].Weighing();
    }

    public void Weights_Add(double[][] weights)
    {
        for (int i = 1, k = 0; i < neurons.Length; i++)
            for (int j = 0; j < neurons[i].Length; j++, k++)
                neurons[i][j].Add_Weights(weights[k]);
    }

    public double[] StartWorking(double[] signals)
    {
        for (int i = 0; i < signals.Length; i++)
        {
            neurons[0][i].Input_Signal(signals[i]);
        }


        Neuron[] neuron_layer = neurons[0][0].next;

        while (neuron_layer != null)
        {
            for (int i = 0; i < neuron_layer.Length; i++)
                neuron_layer[i].Start();

            neuron_layer = neuron_layer[0].next;
        }

        return Get_outputs();
    }

    public double[] Get_outputs()
    {
        int i = neurons.Length - 1;
        double[] outpurs = new double[neurons[i].Length];

        for (int j = 0; j < neurons[i].Length; j++)
            outpurs[j] = neurons[i][j].result;

        return outpurs;
    }
}