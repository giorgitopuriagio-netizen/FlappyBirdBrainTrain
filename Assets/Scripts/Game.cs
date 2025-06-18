using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Game : MonoBehaviour
{
    static public int generation = 0;
    static public int passedSuccessfully = 0;
    static public int FlappyPopulation = 200;
    static public float[] flappiesDeadTime;
    static public double[][][] newGenerationWeights;
    static public Neurons_Network[] neuronNetworks;

    public GameObject flappyPreefab;

    private GeneticAlgorithm flapyEvolution;

    // Start is called before the first frame update
    void Start()
    {
        generation++;
        passedSuccessfully = 0;

        flapyEvolution = new GeneticAlgorithm();
        neuronNetworks = new Neurons_Network[FlappyPopulation];

        flappiesDeadTime = new float[FlappyPopulation];

        Debug.Log("Generation: " + generation);

        FlappyCreate(FlappyPopulation, -7, 0);
    }

    public void FlappyCreate(int quantity, float x, float y)
    {
        for (int i = 0; i < quantity; i++)
        {
            Instantiate(flappyPreefab, new Vector3(x, y, 0), Quaternion.identity);
            //if (i % 10 == 0 && i != 0) y += 0.2f;
        }
    }

    public void GeneticSelection()
    {
        flapyEvolution.EvolutionStart(neuronNetworks);

        newGenerationWeights = flapyEvolution.newGeneration;
    }

    public static void Restart()
    {
        SceneManager.LoadScene(0);
    }
}