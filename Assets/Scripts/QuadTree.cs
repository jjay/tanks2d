using UnityEngine;

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public struct Point {
    int x;
    int y;
}

public enum TerrainType {
    None,
    Grass,
    Tree,
    Stone,
    Water
}

public enum RelativePosition {
    Top,
    Bottom,
    Left,
    Right
}


public struct Position {
    public byte key;
    public Position(byte _key){ key = _key; }
    public Position(string _key){ key = Convert.ToByte(_key); }
    public Position(char _key){ key = Convert.ToByte(_key); }

    public bool HasAdjacent(RelativePosition rel){
        switch (rel){
            case RelativePosition.Top: return (key|1) == 3;
            case RelativePosition.Bottom: return (key|1) == 1;
            case RelativePosition.Left: return (key|2) == 3; 
            case RelativePosition.Right: return (key|2) == 2;
            default: return false;
        }
    }

    public Position Flip(RelativePosition pos){
        if (pos == RelativePosition.Top || pos == RelativePosition.Bottom){
            return new Position( (byte)((2&(~(key&2))) | (1&key)) );
        } else {
            return new Position( (byte)((2&key) | (1&(~(key&1)))) );
        }
    }

    public override string ToString(){
        return Convert.ToString(key);
    }
}

public class QuadPath {

    public static QuadPath empty = new QuadPath(new List<Position>());

    private List<Position> path;

    public QuadPath parent {
        get {
            if (path.Count <= 1) return QuadPath.empty;
            return new QuadPath(path.Take(path.Count - 1).ToList());
        }
    }

    public bool isRoot {
        get { return path.Count == 0; }
    }

    public Position position {
        get { return path.Last(); }
    }

    public Position root {
        get {
            if (path.Count > 0) return path.First();
            throw new UnityException("QuadPath without root");
        }
    }
    
    public QuadPath(string path){
        this.path = new List<Position>();
        foreach (string p in path.Split('/')){
            this.path.Add(new Position(p));
        }
    }

    public QuadPath(QuadPath path){
        this.path = path.path;
    }

    public QuadPath(List<Position> path){
        this.path = path;
    }

    public QuadPath Grow(Position root){
        List<Position> newPath = new List<Position>(path);
        newPath.Insert(0, root);
        return new QuadPath(newPath);
    }
    
    public QuadPath Grow(RelativePosition withFreeAdjacent){
        if (withFreeAdjacent == RelativePosition.Top || withFreeAdjacent == RelativePosition.Right){
            return Grow(new Position(2));
        } else {
            return Grow(new Position(1));
        }
    }

    public bool HasAdjacent(RelativePosition pos){
        if (path.Count == 0) return false;
        foreach (Position currentPos in path.Reverse<Position>()){
            if (currentPos.HasAdjacent(pos)) return true;
        }
        return false;
    }

    public QuadPath FindAdjacentPath(RelativePosition adjacent){
        if (path.Count == 0) return null;
        List<Position> sharedPath = new List<Position>();
        bool adjacentFound = false;
        foreach (Position pos in path.Reverse<Position>()){
            if (adjacentFound){
                sharedPath.Add(pos);
            } else {
                sharedPath.Add(pos.Flip(adjacent));
            }
            if (pos.HasAdjacent(adjacent)){
                adjacentFound = true;
            }
        }
        return new QuadPath(sharedPath.Reverse<Position>().ToList());
    }

    public override string ToString(){
        return String.Join("/", path.Select(pos => pos.ToString()).ToArray());
    }
}

delegate void SaveAction(BinaryWriter writer);

public class QNode {

    public const int BLOCK_SIZE = 15;

    public QuadPath path;
    public int[] weights = {0, 0, 0, 0};
    public int totalWeights {
        get { return weights[0] + weights[1] + weights[2] + weights[3]; }
    }

    private QTree tree;

    public Dictionary<Vector3, TerrainType> terrain;


    public QNode(QTree tree, QuadPath path){
        this.tree = tree;
        this.path = path;
    }
    
    public QNode(QTree tree, string path){
        this.tree = tree;
        this.path = new QuadPath(path);
    }

    public void GenerateTerrain(){
        terrain = new Dictionary<Vector3, TerrainType>();
        for (int i = 0; i < 50; i++){
            Vector3 pos;
            do {
                int x = UnityEngine.Random.Range(0, BLOCK_SIZE);
                int y = UnityEngine.Random.Range(0, BLOCK_SIZE);
                pos = new Vector3((float)x, (float)y, 0);
            } while (terrain.ContainsKey(pos));

            float chance = UnityEngine.Random.Range((float)0, (float)1);
            if (chance < 0.1) terrain[pos] = TerrainType.Tree;
            else if (chance < 0.4) terrain[pos] = TerrainType.Grass;
            else if (chance < 0.5) terrain[pos] = TerrainType.Water;
            else terrain[pos] = TerrainType.Stone;
        }
    }

    public void Load(){
        string filePath = Path.Combine(tree.rootLocation, path.ToString());
        if (File.Exists(filePath+".terrain")){
            // try to use compression here
            // System.IO.Compression doesn't seems avaible inside Unity
            FileStream stream = new FileStream(filePath+".terrain", FileMode.Open);
            BinaryReader reader = new BinaryReader(stream);
            LoadTerrainNode(reader);
            reader.Close();
        } else if ( File.Exists(Path.Combine(filePath, "info")) ){
            FileStream stream = new FileStream(Path.Combine(filePath, "info"), FileMode.Open);
            BinaryReader reader = new BinaryReader(stream);
            LoadInfoNode(reader);
            reader.Close();
        }
    }

    private void LoadTerrainNode(BinaryReader reader){
        for (float x = 0; x < BLOCK_SIZE; x++){
            for (float y = 0; y < BLOCK_SIZE; y++){
                TerrainType terrainType = (TerrainType)reader.ReadByte();
                if (terrainType == TerrainType.None) continue;
                if (terrain == null) terrain = new Dictionary<Vector3, TerrainType>();
                terrain[new Vector3(x, y, 0)] = terrainType;
            }
        }
    }

    private void LoadInfoNode(BinaryReader reader){
        for (int i = 0; i<4; i++) weights[i] = reader.ReadInt32();
    }

    public void Save(){
        string nodePath = Path.Combine(tree.rootLocation, path.ToString());
        SaveAction save;
        if (terrain == null){
            nodePath = Path.Combine(nodePath, "info");
            save = SaveInfoNode;
        } else {
            nodePath += ".terrain";
            save = SaveTerrainNode;
        }

        // try to use compression for datanodes
        // System.IO.Compression doesn't seems avaible inside Unity
        Directory.CreateDirectory(Path.GetDirectoryName(nodePath));
        FileStream stream = new FileStream(nodePath, FileMode.Create);
        BinaryWriter writer = new BinaryWriter(stream);
        save(writer);
        writer.Close();
    }

    private void SaveInfoNode(BinaryWriter writer){
        for (int i = 0; i<4; i++) writer.Write(weights[i]);
    }

    private void SaveTerrainNode(BinaryWriter writer){
        for ( float x=0; x<BLOCK_SIZE; x++){
            for ( float y=0; y<BLOCK_SIZE; y++){
                Vector3 point = new Vector3(x, y, 0);
                if (terrain.ContainsKey(point)){
                    writer.Write((byte)terrain[point]);
                } else {
                    writer.Write((byte)0);
                }
            }
        }

    }

}

public class QTree {

    public string rootLocation;

    public QTree(string root){
        rootLocation = root;
    }
    
    public void Reparent(Position newParent){
        string[] dirs = Directory.GetDirectories(rootLocation);
        string[] files = Directory.GetFiles(rootLocation);
        QNode info = LoadOrCreate(QuadPath.empty);
        QNode newRootInfo = Create(new QuadPath(newParent.ToString()));
        newRootInfo.weights[newParent.key] = info.totalWeights;
        string tmpDir = Path.Combine(rootLocation, "tmp");
        Directory.CreateDirectory(tmpDir);
        foreach (string file in files){
            File.Move(file, file.Replace(rootLocation, tmpDir));
        }
        foreach (string dir in dirs){
            Directory.Move(dir, dir.Replace(rootLocation, tmpDir));
        }
        Directory.Move(tmpDir, Path.Combine(rootLocation, newParent.ToString()));
        newRootInfo.Save();
    }

    public void UpdateWeights(QNode node){
        do {
            QNode parent = LoadOrCreate(node.path.parent);
            parent.weights[node.path.position.key] = node.totalWeights;
            parent.Save();
            node = parent;
        } while (!node.path.isRoot);
    }

    public QNode GetTerrain(QuadPath path){
        QNode node = LoadOrCreate(path);
        if (node.terrain == null){
            node.GenerateTerrain();
            node.Save();
            UpdateWeights(node);
        }
        return node;
    }

    public QNode GetAdjacentTerrain(QNode terrain, RelativePosition adjacent){
        if (terrain.path.HasAdjacent(adjacent)) return GetTerrain(terrain.path.FindAdjacentPath(adjacent));
        terrain.path = terrain.path.Grow(adjacent);
        Reparent(terrain.path.root);
        return GetTerrain(terrain.path.FindAdjacentPath(adjacent));
    }

    public QNode Create(QuadPath path){
        return new QNode(this, path);
    }

    public QNode LoadOrCreate(QuadPath path){
        QNode node = Create(path);
        node.Load();
        return node;

    }

    public void Clear(){
        Directory.Delete(rootLocation, true);
    }
}
