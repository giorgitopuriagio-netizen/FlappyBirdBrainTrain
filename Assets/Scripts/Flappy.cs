using System.Collections.Generic;
using System;
using UnityEngine;

public class Flappy : MonoBehaviour
{
    static public int deads = 0;
    static public int indexCounter = 0;

    [SerializeField] private float flySpeed = 5f;
    private Rigidbody2D rigidBody;

    public int index;
    public int rank;
    public float fitness { get; private set; }

    public Game gameManager;
    public Neurons_Network brain;

    double[][] fileWeights;

    private bool alive;

    private void Start()
    {
        rigidBody = GetComponent<Rigidbody2D>();
        gameManager = GameObject.FindGameObjectWithTag("GameController").GetComponent<Game>();

        alive = true;
        index = indexCounter;

        //GetWeightsAtFille();

        brain = new Neurons_Network(6, 40, 1, 3, fileWeights);

        if (Game.generation > 1) brain = new Neurons_Network(6, 40, 1, 3, Game.newGenerationWeights[index]);
        else brain = new Neurons_Network(6, 40, 1, 3);

        Debug.Log(brain);

        indexCounter++;
    }

    private void GetWeightsAtFille()
    {
        string filePath = Application.persistentDataPath + "/Bot " + 20 + " Weights.json";
        string file = System.IO.File.ReadAllText(filePath);
        string strNumber = "";

        int length = 0;

        for (int i = 0; i < file.Length; i++)
        {
            if (file[i].ToString() == "|") length++;
        }

        fileWeights = new double[length][];

        length = 0;

        for (int i = 0, j = 0; i < file.Length; i++)
        {
            if (file[i].ToString() == ",") length++;
            else if(file[i].ToString() == "|")
            {
                fileWeights[j] = new double[length];
                j++;
                length = 0;
            }
        }

        for (int i = 0, j = 0, k = 0; i < file.Length; i++)
        {
            string strSimbol = file[i].ToString();

            if (strSimbol == ",")
            {
                fileWeights[j][k] = Double.Parse(strNumber);
                
                k++;
                strNumber = "";
            }
            else if (strSimbol == "|")
            {
                j++;
                k = 0;
            }
            else if (strSimbol != " ") strNumber += strSimbol;
        }

        /*string filePath = Application.persistentDataPath + "/Flappies " + 1 + " Generetion Chromosomes.json";
    string file = System.IO.File.ReadAllText(filePath).ToString();

    //double[] fileWeights = Array.ConvertAll(file.Split(','), new Converter<string, double>(Double.Parse));

    //double[] fileWeights = file.Split(',').Select(double.Parse).ToArray();

    Debug.Log(file);*/

        //Debug.Log(_fileWeights[0]);
    }

    // Update is called once per frame
    void Update()
    {
        if (alive)
        {
            //fitness = Time.time;
            ChekingPosition();
            Think();
        }

        /*if (Input.GetKey(KeyCode.Space))
        {
            rigidBody.velocity = Vector2.up * flySpeed;
        }*/
    }

    GameObject Enemy()
    {
        List<GameObject> enemyList = EnemySpawn.enemyList;

        foreach (GameObject enemy in enemyList)
        {
            if (this.transform.position.x < enemy.transform.position.x + 0.5) return enemy;
            else
            {
                Debug.Log("Remove");
                Debug.Log("resistance overcome: " + ++Game.passedSuccessfully);
                enemyList.Remove(enemy);
                return Enemy();
            }
        }

        return null;
    }

    void Think()
    {
        GameObject enemy = Enemy();

        if (enemy != null)
        {
            GameObject up = enemy;
            GameObject down = enemy;

            double[] inputs = new double[6];

            for(int i = 0; i < enemy.transform.childCount; i++)
            {
                if(enemy.transform.GetChild(i).name == "Up") up = enemy.transform.GetChild(i).gameObject;
                else if(enemy.transform.GetChild(i).name == "Down") down = enemy.transform.GetChild(i).gameObject;
            }            

            inputs[0] = Vector2.Distance(gameObject.transform.position, up.transform.position);

            inputs[1] = Vector2.Distance(gameObject.transform.position, down.transform.position);

            inputs[2] = Vector2.Distance(gameObject.transform.position, enemy.transform.position);

            inputs[3] = this.transform.position.y;
            inputs[4] = 6 - this.transform.position.y;
            inputs[5] = this.transform.position.y - (-6);

            double[] output = brain.StartWorking(inputs);

            if (output[0] > 0.5) Fly();
        }


    }

    void Fly()
    {
        rigidBody.velocity = Vector2.up * flySpeed;
    }

    void ChekingPosition()
    {
        if(this.transform.position.y >= 6 || this.transform.position.y <= -6)
        {
            Dead();
        }
    }

    void Dead()
    {
        deads++;
        this.alive = false;

        this.rank = deads;

        Game.neuronNetworks[this.rank - 1] = this.brain;

        //gameManager.flappiesBrain.Add(brain);

        Destroy(gameObject);

        Game.flappiesDeadTime[rank - 1] = Time.time;

        if (deads == Game.FlappyPopulation)
        {
            deads = 0;
            indexCounter = 0;

            gameManager.GeneticSelection();

            Game.Restart();
        }
    }

    void OnTriggerEnter2D(Collider2D enemy)
    {
        if (enemy.tag == "Enemy")
        {
            Dead();
        }
    }
}