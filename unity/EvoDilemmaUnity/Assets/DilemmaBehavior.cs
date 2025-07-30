using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using UnityEngine.Networking;

public class DilemmaBehavior : MonoBehaviour
{
    [SerializeField] private GameObject agentPrefab;
    [SerializeField] private GameObject edgePrefab;
    [SerializeField] private GameObject backgroundObject;

    private List<GameObject> agents = new List<GameObject>();
    private List<(int, int)> edges;

    [SerializeField] private float jitterStrength = 22f; // Variable that scatters our agents (without this, they'll arrange themselves into a perfect circle)

    private void Start()
    {
        LoadGraph();
        SpawnAgents();
        StartCoroutine(SpawnModels());
        DrawEdges();
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
        using (UnityWebRequest www = new UnityWebRequest("http://localhost:8080/models_init", "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            
            yield return www.SendWebRequest();
            
            if(www.result != UnityWebRequest.Result.Success){
                Debug.LogError($"Model init failed: {www.error}");
            }
            else{
                Debug.Log("Models successfully initialized on server!");
                Debug.Log($"Server returned: {www.downloadHandler.text}"); // This bit is honestly just to confirm that the correct number of agents have been created on the server
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
}