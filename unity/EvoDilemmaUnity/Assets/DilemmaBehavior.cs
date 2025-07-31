using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using UnityEngine.Networking;
using UnityEngine.UI;
using System.Linq;

public class DilemmaBehavior : MonoBehaviour
{
    [SerializeField] private GameObject agentPrefab;
    [SerializeField] private GameObject edgePrefab;
    [SerializeField] private GameObject backgroundObject;
    [SerializeField] private Button generationButton;

    private List<GameObject> agents = new List<GameObject>();
    private List<(int, int)> edges;
    public static List<(string, int)> agentBehaviorList = new List<(string, int)>(); // Public static as we need to access it in our AgentScript

    [SerializeField] private float jitterStrength = 22f; // Variable that scatters our agents (without this, they'll arrange themselves into a perfect circle)

    private void Start()
    {
        LoadGraph();
        SpawnAgents();
        StartCoroutine(SpawnModels());
        DrawEdges();
        generationButton.onClick.AddListener(OnGenerationProgressionClicked);
    }
    void LoadGraph()
    {
        TextAsset textAsset = Resources.Load<TextAsset>("ws_graph_edges"); // The JSON file we initialized under the generate_agent_graph python processes script
        edges = JsonConvert.DeserializeObject<List<List<int>>>(textAsset.text).ConvertAll(edge => (edge[0], edge[1]));
        Debug.Log($"Loaded {edges.Count} edges");
    }

    void SpawnAgents()
    {
        int numAgents = 400;
        float radius = 10f;
         
        for(int i = 0; i < numAgents; i++){
            float angle = i * Mathf.PI * 2 / numAgents;
            Vector2 basePos = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            Vector2 jitter = Random.insideUnitCircle * jitterStrength;
            Vector2 pos = basePos + jitter;

            GameObject agent = Instantiate(agentPrefab, new Vector3(pos.x, pos.y, 0), Quaternion.identity);
            agent.name = $"Agent_{i}";
            agents.Add(agent);
            agent.GetComponent<AgentScript>().agentID = $"agent_{i}";
        }
    }
    // I know my naming isn't the best, but the difference between SpawnModels and SpawnAgents is that this simply initializes the random pytorch models on our django server
    IEnumerator SpawnModels()
    {
        string jsonRequest = $"{{\"num_agents\": {agents.Count}}}";
        
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonRequest);
        using (UnityWebRequest fetchSpawn = new UnityWebRequest("http://localhost:8080/models_init", "POST"))
        {
            fetchSpawn.uploadHandler = new UploadHandlerRaw(bodyRaw);
            fetchSpawn.downloadHandler = new DownloadHandlerBuffer();
            fetchSpawn.SetRequestHeader("Content-Type", "application/json");
            
            yield return fetchSpawn.SendWebRequest();
            
            if(fetchSpawn.result != UnityWebRequest.Result.Success){
                Debug.LogError($"Model init failed: {fetchSpawn.error}");
            }
            else{
                Debug.Log("Models successfully initialized on server!");
                Debug.Log($"Server returned: {fetchSpawn.downloadHandler.text}"); // This bit is honestly just to confirm that the correct number of agents have been created on the server
            }
        }
    }

    void DrawEdges()
    {
        foreach(var (a, b) in edges){
            Vector3 posA = agents[a].transform.position;
            Vector3 posB = agents[b].transform.position;

            GameObject edge = Instantiate(edgePrefab);
            var lineRenderer = edge.GetComponent<LineRenderer>();
            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, new Vector3(posA.x, posA.y, 0f));
            lineRenderer.SetPosition(1, new Vector3(posB.x, posB.y, 0f));
            lineRenderer.startWidth = 0.05f;
            lineRenderer.endWidth = 0.05f;

            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.startColor = Color.gray;
            lineRenderer.endColor = Color.gray;

            lineRenderer.sortingLayerName = "Edges";  
            lineRenderer.sortingOrder = 0;

            var agentA = agents[a].GetComponent<AgentScript>();
            var agentB = agents[b].GetComponent<AgentScript>();

            if(agentA && agentB){
                agentA.connectedAgents.Add(agents[b]);
                agentB.connectedAgents.Add(agents[a]);

                agentA.connectedEdges.Add(lineRenderer);
                agentB.connectedEdges.Add(lineRenderer);
            }
        }
    }
    void OnGenerationProgressionClicked()
    {
        Debug.Log("Generation Progression button clicked!");
        StartCoroutine(ProgressGeneration());
        // Run Progress Generation if other conditions are met
    }
    IEnumerator ProgressGeneration()
    {
        using (UnityWebRequest fetchGeneration = UnityWebRequest.Get("http://localhost:8080/play_dilemma"))
        {
            yield return fetchGeneration.SendWebRequest();

            if(fetchGeneration.result != UnityWebRequest.Result.Success){
                Debug.LogError($"Generation progression failed: {fetchGeneration.error}");
            }
            else{
                string responseText = fetchGeneration.downloadHandler.text;
                Debug.Log("Generation progression successful!");
                Debug.Log($"Server returned: {responseText}");

                var generationData = JsonConvert.DeserializeObject<GenerationResponse>(responseText);
                // GenerationData includes the following: 
                // generationData.actions
                // generationData.fitness_scores
                // generationData.general_scores

                // Who we'll keep from this generation.
                // Now I contemplated between progressing generations either based on global score or between score among smaller subsets of say, 4 agents.
                // I chose global progression as it is, ahem, easier to do, and that we can still obtain variance akin to local tournaments given the variety of opponents each agent plays due to our Watts-Strogatz network. 
                var sortedTopFitness = generationData.fitness_scores.OrderByDescending(pair => pair.Value).Take((int)(generationData.fitness_scores.Count * 0.25)).ToDictionary(pair => pair.Key, pair => pair.Value);
                
                // For visualization, we'll save if certain agents lean more towards being stealers/splitters. 
                foreach(var agentActions in generationData.actions){
                    int zeros = agentActions.Value.Count(actions => actions == 0);
                    int ones = agentActions.Value.Count(actions => actions == 1);
                    int behavior = (zeros >= ones) ? 0 : 1;
                    agentBehaviorList.Add((agentActions.Key, behavior));
                }
                // Without this line/logic we would encounter an error where whenever an agent is clicked off, their color turns gray. 
                AgentScript.agentBehaviorList = agentBehaviorList;

                // Now we'll loop through our agent GameObjects and set their color to Blue(Leans more towards splitting) and Red(Leans more towards stealing)
                foreach(var agent in agents){
                    var agentScript = agent.GetComponent<AgentScript>();
                    string agentId = agentScript.agentID;

                    var behaviorEntry = agentBehaviorList.FirstOrDefault(entry => entry.Item1 == agentId);

                    if(behaviorEntry != default){
                        Color colorToSet = (behaviorEntry.Item2 == 0) ? Color.blue : Color.red;
                        agentScript.SetColor(colorToSet);
                    }
                }
                
            }
        }

    }
}

public class GenerationResponse{ // Just a little custom type for QOL ig
    public string message;
    public Dictionary<string, List<int>> actions;
    public Dictionary<string, float> fitness_scores;
    public Dictionary<string, int> general_scores;
}