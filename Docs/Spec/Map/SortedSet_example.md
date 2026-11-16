##  SortedSet example

```csharp
using System;
using System.Collections.Generic;


namespace Algorithm
{
    
    public class Program
    {
        public class Mycompare : IComparer<List<int>>
        {
        	// 비용 기준으로 오름차순
            public int Compare(List<int> x, List<int> y)
            {
                if (x[0] == y[0]) return x[1] - y[1];
                return x[0] - y[0];
            }
        }

        public static int[,] dist = new int[20, 20]; //[시작노드, 도착노드]
        public static List<List<int>>[] node = new List<List<int>>[20]; // 노드 정보
		
        // 다익스트라 알고리즘
        public static void Dijkstra(int sx)
        {
            SortedSet<List<int>> pq = new SortedSet<List<int>>(new Mycompare());
            dist[sx,sx] = 0;
            pq.Add(new List<int>() { 0, sx });

            while (pq.Count > 0)
            {
                List<int> cnt = pq.First();
                pq.Remove(cnt);

                int x = cnt[1];
                int cost = cnt[0];

                if (dist[sx, x] < cost) continue;
                for (int i = 0; i < node[x].Count; i++)
                {
                    int next = node[x][i][0];
                    int nCost = node[x][i][1] + cost;
                    if (dist[sx,next] > nCost)
                    {
                        dist[sx, next] = nCost;
                        pq.Add(new List<int>() { nCost, next });
                    }

                }
            }
        }
        public static void Main(String[] args)
        {
        	//초기화
            for (int i = 0; i < 20; i++) node[i] = new List<List<int>>();
            
            // dist 배열 큰 값으로 맞춰줌
            for(int i = 0; i < 20; i++)
                for (int j = 0; j < 20; j++) dist[i, j] = 9999999;
            
            
            // 노드 정보 담기
            node[0].Add(new List<int>(){3,4});
            node[1].Add(new List<int>(){0,3});
            node[1].Add(new List<int>(){2,1});
            node[1].Add(new List<int>(){3,3});
            node[2].Add(new List<int>(){0,2});
            node[2].Add(new List<int>(){3,1});
            node[3].Add(new List<int>(){2,3});
            
            for(int i = 0; i <= 3; i++) Dijkstra(i);

            for (int i = 0; i <= 3; i++)
            {
                for (int j = 0; j <= 3; j++)
                {
                    if (i == j) continue;
                    Console.WriteLine($"{i}번 노드에서 {j}번 노드로 갈 때의 최소 비용 = {dist[i,j]}");
                }
            }
        }
    }
}
```