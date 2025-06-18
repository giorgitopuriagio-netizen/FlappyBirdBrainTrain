using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

using Random = UnityEngine.Random;

public class GeneticAlgorithm : MonoBehaviour
{
    private Neurons_Network[] individuals;

    private int genesNum;
    private int generation;
    private int parentsNum;
    private int chromosomeLength;
    
    private double[][][] chromosomes;
    private double[][][] fromSelection;

    public double[][][] newGeneration { get; protected set; }
    
    public GeneticAlgorithm()
    {
        generation = 1;
        parentsNum = 10;
    
        chromosomes = null;
        fromSelection = null;
        newGeneration = null;
    }
    
    public void EvolutionStart(Neurons_Network[] individuals)
    {
        generation++;
        this.individuals = individuals;
        this.newGeneration = new double[this.individuals.Length][][];
        
        this.chromosomes = GetChromosomes(this.individuals);

        SaveToJson();
        //???????????? (???????) ?????????????? ????????!!!

        Grouping();
        Crosovering();
        Mutation(MutationChance());
    }

    private double[][][] GetChromosomes(Neurons_Network[] individuals)
    {
        double[][][] chromosomes = new double[individuals.Length][][];
        int lengthCounter = 0;

        for(int i = 0; i < individuals[0].neurons.Length; i++)
            for(int j = 0; j < individuals[0].neurons[i].Length; j++)
                if(individuals[0].neurons[i][j].weights != null) lengthCounter++;

        for(int i = 0; i < chromosomes.Length; i++)
            chromosomes[i] = new double[lengthCounter][];

        for(int i = 0; i < individuals.Length; i++)
            for(int j = 0, l = 0; j < individuals[i].neurons.Length; j++)
                for(int k = 0; k < individuals[i].neurons[j].Length; k++)
                    if(individuals[i].neurons[j][k].weights != null)
                    {
                        chromosomes[i][l] = individuals[i].neurons[j][k].weights;
                        l++;
                    }

        return chromosomes;
    }

    private void Grouping()
    {
        fromSelection = new double[parentsNum][][];
        int best = 50 * parentsNum / 100;
        int medium = 30 * parentsNum / 100;


        fromSelection[0] = chromosomes[chromosomes.Length - 1];
    
        for (int i = 1; i < fromSelection.Length; i++)
        {
            if (i < best)
            {
                do
                {
                    fromSelection[i] = ChackEqualInSelection(chromosomes[Random.Range(95 * chromosomes.Length / 100, chromosomes.Length)]);
                }
                while (fromSelection[i] == null);

            }
            else if (i < medium)
            {
                do
                {
                    fromSelection[i] = ChackEqualInSelection(chromosomes[Random.Range(90 * chromosomes.Length / 100, 95 * chromosomes.Length / 100)]);
                }
                while (fromSelection[i] == null);
            }
            else
            {
                do
                {
                    fromSelection[i] = ChackEqualInSelection(chromosomes[Random.Range(85 * chromosomes.Length / 100, 90 * chromosomes.Length / 100)]);
                }
                while (fromSelection[i] == null);
            }
        }
    }

    private double[][] ChackEqualInSelection(double[][] chromosome)
    {
        for (int i = 0; i < fromSelection.Length; i++)
            if(fromSelection[i] == null) return chromosome;
            else if (fromSelection[i].Equals(chromosome)) return null;

        return chromosome;
    }

    private void Crosovering()
    {
        for (int i = 0; i < chromosomes.Length - parentsNum; i++)
        {
            double[][] chromosome1 = null;
            double[][] chromosome2 = null;
    
            newGeneration[i] = new double[this.chromosomes[i].Length][];
    
            do
            {
                chromosome1 = fromSelection[Random.Range(0, parentsNum)];
                chromosome2 = fromSelection[Random.Range(0, parentsNum)];
            }
            while (chromosome1.Equals(chromosome2));

            int point = Random.Range(1, chromosome1[0].Length);
            
            for (int j = 0; j < point; j++)
                newGeneration[i][j] = chromosome1[j];

            for (int j = point; j < chromosome2.Length; j++)
                newGeneration[i][j] = chromosome2[j];
        }

        for (int i = 0, j = chromosomes.Length - parentsNum; j < chromosomes.Length; i++, j++)
        {
            newGeneration[j] = fromSelection[i];
        }
    }

    private int MutationChance()
    {
        float bestTime = Game.flappiesDeadTime[Game.flappiesDeadTime.Length - 1];

        for(int i = 85 * chromosomes.Length / 100; i < Game.flappiesDeadTime.Length - 1; i++)
            if (bestTime == Game.flappiesDeadTime[i])
            {
                Debug.Log("Mutation: " + "100%");
                return 100;
            }

        Debug.Log("Mutation: " + "50%");

        return 50;
    }
    
    private void Mutation(int mutationChans = 50)
    {
        for (int i = 0; i < newGeneration.Length - parentsNum; i++)
        {
            if (Random.Range(0, 100) <= mutationChans)
            {
                int mutable = Random.Range(0, newGeneration[i].Length);
                double[] newGene = new double[newGeneration[i][mutable].Length];

                for (int j = 0; j < newGeneration[i][mutable].Length; j++)
                    if (Random.Range(0, 2) == 1) newGene[j] = Random.Range(0.0f, 1.0f);
                    else newGene[j] = Random.Range(0.0f, 1.0f) * (-1);

                newGeneration[i][mutable] = newGene;
            }
        }

        /*if(mutationChans < 100)
        {
            for (int i = 0; i < newGeneration.Length; i++)
            {
                if (Random.Range(0, 100) <= mutationChans)
                {
                    int mutable = Random.Range(0, newGeneration[i].Length);
                    double[] newGene = new double[newGeneration[i][mutable].Length];

                    for (int j = 0; j < newGeneration[i][mutable].Length; j++)
                        if (Random.Range(0, 2) == 1) newGene[j] = Random.Range(0.0f, 1.0f);
                        else newGene[j] = Random.Range(0.0f, 1.0f) * (-1);

                    newGeneration[i][mutable] = newGene;
                }
            }
        }

        else
        {
            for (int i = 0; i < newGeneration.Length; i++)
            {
                if (Random.Range(0, 100) <= mutationChans)
                {
                    int mutationPoint = Random.Range(0, newGeneration[i].Length);

                    for (int mutable = mutationPoint; mutable < newGeneration[i].Length; mutable++)
                    {
                        double[] newGene = new double[newGeneration[i][mutable].Length];

                        for (int j = 0; j < newGeneration[i][mutable].Length; j++)
                            if (Random.Range(0, 2) == 1) newGene[j] = Random.Range(0.0f, 1.0f);
                            else newGene[j] = Random.Range(0.0f, 1.0f) * (-1);

                        newGeneration[i][mutable] = newGene;
                    }
                }
            }
        }*/
    }

    void SaveToJson()
    {
        //Chromosomes toJson = new Chromosomes();

        string chromosomesData = "";

        /*for (int i = 0; i < chromosomes.Length; i++)
        {
            */
            for (int j = 0; j < chromosomes[chromosomes.Length - 1].Length; j++)
            {
                for (int k = 0; k < chromosomes[chromosomes.Length - 1][j].Length; k++)
                {
                    chromosomesData += chromosomes[chromosomes.Length - 1][j][k] + ", ";
                }

               chromosomesData += "| ";
            }
        /*}*/




        string filePath = Application.persistentDataPath + "/Bot " + (Game.generation) + " Weights.json";

        print(filePath);

        System.IO.File.WriteAllText(filePath, chromosomesData);

        print(filePath);
    }
}

/*
class Chromosomes
{
    public List<double> weights = new List<double>();

    public void AddToList(double[] arrey)
    {
        for(int i = 0; i < arrey.Length; i++) 
        {
            weights.Add(arrey[i]);
        }
    }
}
*/