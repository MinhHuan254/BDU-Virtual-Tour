using System;
using System.Collections.Generic;
using UnityEngine;

public class MoverCar : MonoBehaviour
{
    public float speed = 4f;

    private List<Transform> route;
    private int targetIndex;
    private Action onFinish;

    public void MoveAlong(List<Transform> path, Action finishCallback = null)
    {
        route = path;
        targetIndex = 0;
        onFinish = finishCallback;
    }

    void Update()
    {
        if (route == null || targetIndex >= route.Count) return;

        Transform target = route[targetIndex];

        transform.position = Vector3.MoveTowards(
            transform.position,
            target.position,
            speed * Time.deltaTime
        );

        transform.LookAt(target);

        if (Vector3.Distance(transform.position, target.position) < 0.1f)
        {
            targetIndex++;

            if (targetIndex >= route.Count)
            {
                if (onFinish != null)
                    onFinish();

                route = null;
            }
        }
    }
}