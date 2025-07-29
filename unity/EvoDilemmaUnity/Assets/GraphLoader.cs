using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

public class GraphLoader : MonoBehaviour
{
    [SerializeField] private GameObject agentPrefab;
    [SerializeField] private GameObject edgePrefab;

    private List<GameObject> agents = new List<GameObject>();
    private List<(int, int)> edges;

    private void Start()
    {
        LoadGraph();
        SpawnAgents();
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
        int numAgents = 200;
        float radius = 10f;
        float jitterStrength = 22f; // Variable that scatters our agents (without this, they'll arrange themselves into a perfect circle)

        for(int i = 0; i < numAgents; i++){
            float angle = i * Mathf.PI * 2 / numAgents;
            Vector2 basePos = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            Vector2 jitter = Random.insideUnitCircle * jitterStrength;
            Vector2 pos = basePos + jitter;

            GameObject agent = Instantiate(agentPrefab, new Vector3(pos.x, pos.y, 0), Quaternion.identity);
            agent.name = $"Agent_{i}";
            agents.Add(agent);
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
            lineRenderer.SetPosition(0, posA);
            lineRenderer.SetPosition(1, posB);
            lineRenderer.startWidth = 0.05f;
            lineRenderer.endWidth = 0.05f;

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