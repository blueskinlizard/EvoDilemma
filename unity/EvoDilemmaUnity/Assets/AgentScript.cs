using System.Collections;
using System.Collections.Generic;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;


public class AgentScript : MonoBehaviour
{
    public string agentID;

    private List<int> agentHistory; // List of agent's actions
    private List<int> enemyHistory; // General list of all of the agent's opponent's actions

    private SpriteRenderer sr;
    private Color originalColor;

    public List<GameObject> connectedAgents = new List<GameObject>();
    public List<LineRenderer> connectedEdges = new List<LineRenderer>();

    private static List<AgentScript> lastSelectedAgents = new List<AgentScript>();
    private static List<LineRenderer> lastSelectedEdges = new List<LineRenderer>();

    private static AgentScript[] allAgents;
    private static LineRenderer[] allEdges;

    public static List<(string, int)> agentBehaviorList = new List<(string, int)>();

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        originalColor = sr.color;
        sr.sortingLayerName = "Agents";
        sr.sortingOrder = 0;

        // Cache all of our agents and lines initially
        if(allAgents == null || allEdges == null){
            allAgents = FindObjectsOfType<AgentScript>();
            allEdges = FindObjectsOfType<LineRenderer>();
        }
    }

    void OnMouseDown(){
        Debug.Log($"{agentID} clicked");
        ResetPreviousSelection();
        AgentClickAction();
    }

    void ResetPreviousSelection()
    {
        foreach(var edge in lastSelectedEdges){
            edge.startColor = new Color(0.5f, 0.5f, 0.5f, 0.3f); 
            edge.endColor = new Color(0.5f, 0.5f, 0.5f, 0.3f);
            edge.startWidth = 0.02f;
            edge.endWidth = 0.02f;
        }
        lastSelectedEdges.Clear();

        foreach(var agent in lastSelectedAgents){
            RestoreAgentBehaviorColor(agent);
        }
        lastSelectedAgents.Clear();

        foreach(var agent in allAgents){
            RestoreAgentBehaviorColor(agent, setAlpha: true); 
        }
    }

    void AgentClickAction()
    {
        // Make everything NOT clicked more transparent after an agent selection 
        foreach(var agent in allAgents)
        {
            if(agent != this && !connectedAgents.Contains(agent.gameObject)){
                Color c = agent.sr.color;
                c.a = 0.3f; // transparent
                agent.sr.color = c;
            }
            else{
                Color c = agent.sr.color;
                c.a = 1f;
                agent.sr.color = c;
            }
        }

        foreach(var edge in allEdges)
        {
            if(connectedEdges.Contains(edge)){
                edge.startColor = Color.green;
                edge.endColor = Color.green;
                edge.startWidth = 0.1f; // Increase line thickness after agent clicked on
                edge.endWidth = 0.1f;

                lastSelectedEdges.Add(edge);
            }
            else{
                Color startC = edge.startColor;
                Color endC = edge.endColor;
                startC.a = 0.3f;
                endC.a = 0.3f;
                edge.startColor = startC;
                edge.endColor = endC;
                edge.startWidth = 0.02f;
                edge.endWidth = 0.02f;
            }
        }

        sr.color = new Color(originalColor.r, originalColor.g, originalColor.b, 1f);
        sr.color = Color.yellow;

        foreach(var go in connectedAgents){
            var otherAgent = go.GetComponent<AgentScript>();
            if(otherAgent != null){
                otherAgent.SetColor(new Color(1f, 0.4f, 0.7f, 1f));
                lastSelectedAgents.Add(otherAgent);
            }
        }

        lastSelectedAgents.Add(this);
    }

    public void SetColor(Color color){
        sr.color = color;
    }
    void RestoreAgentBehaviorColor(AgentScript agent, bool setAlpha = false)
    {
        var behaviorEntry = agentBehaviorList.FirstOrDefault(entry => entry.Item1 == agent.agentID);

        if(behaviorEntry != default && agentBehaviorList.Contains(behaviorEntry)){
            Color behaviorColor = (behaviorEntry.Item2 == 0) ? Color.blue : Color.red;
            if (setAlpha)
            {
                behaviorColor.a = 1f;
            }
            agent.SetColor(behaviorColor);
        }
        else{
            agent.SetColor(Color.gray); 
        }
    }
}