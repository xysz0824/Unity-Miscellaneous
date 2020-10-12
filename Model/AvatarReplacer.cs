using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using Sirenix.OdinInspector;
#endif

public class AvatarReplacer : MonoBehaviour
{
    Animator animator;
    Transform bodyPart;
    Transform clothesPart;
    Transform footballPart;
    Transform eyePart;
    Transform hairPart;
    Transform headPart;
    List<Transform> newBodyParts;
    List<Transform> newBodyBones;
    List<Transform> newHeadParts;
    List<Transform> newHeadBones;
    Material originalMaterial;
    public List<Transform> SearchRootBones;
#if UNITY_EDITOR
    [SerializeField]
    private GameObject TestHead;
    [Button("替换新头")]
    void ReplaceNewHead()
    {
        ReplaceHead(TestHead);
    }
    [Button("恢复旧头")]
    void RecoverOldHead()
    {
        RecoverHead();
    }
    [SerializeField]
    private GameObject TestBody;
    [Button("替换身体")]
    void ReplaceNewBody()
    {
        ReplaceBody(TestBody);
    }
    [Button("恢复身体")]
    void RecoverOldBody()
    {
        RecoverBody();
    }
    [SerializeField]
    private GameObject TestFullSuit;
    [Button("替换全身")]
    void ReplaceNewFullSuit()
    {
        ReplaceFullSuit(TestFullSuit);
    }
    [Button("恢复全身")]
    void RecoverOldFullSuit()
    {
        RecoverFullSuit();
    }
    [SerializeField]
    private Material TestCloth;
    [Button("替换全身")]
    void ReplaceNewCloth()
    {
        ReplaceCloth(TestCloth);
    }
    [Button("恢复全身")]
    void RecoverOldCloth()
    {
        RecoverCloth();
    }
    [SerializeField]
    private GameObject TestBodyPart;
    [Button("附加到身体")]
    void AttachBodyPart()
    {
        AttachBodyPart(TestBodyPart);
    }
    [Button("解除附加")]
    void DetachBodyPartTest()
    {
        DetachBodyPart();
    }
#endif
    private void Init()
    {
        animator = GetComponent<Animator>();
        //Body Parts
        bodyPart = transform.Find("body_01");
        clothesPart = transform.Find("clothes_01");
        footballPart = transform.Find("football_01");
        newBodyParts = new List<Transform>();
        newBodyBones = new List<Transform>();
        //Head Parts
        eyePart = transform.Find("eye_01");
        hairPart = transform.Find("hair_01");
        headPart = transform.Find("head_01");
        newHeadParts = new List<Transform>();
        newHeadBones = new List<Transform>();
    }
    Transform FindSameBone(Transform currentBone, string name)
    {
        if (currentBone.name == name)
        {
            return currentBone;
        }
        var count = currentBone.childCount;
        for (int i = 0; i < count; ++i)
        {
            var childBone = currentBone.GetChild(i);
            if (childBone.name == name)
            {
                return childBone;
            }
        }
        for (int i = 0; i < count; ++i)
        {
            var childBone = currentBone.GetChild(i);
            var sameBone = FindSameBone(childBone, name);
            if (sameBone != null)
            {
                return sameBone;
            }
        }
        return null;
    }
    Transform FindSameBone(string name)
    {
        foreach (var searchRootBone in SearchRootBones)
        {
            var sameBone = FindSameBone(searchRootBone, name);
            if (sameBone != null)
            {
                return sameBone;
            }
        }
        return null;
    }
    void BatchBone(Transform newBone)
    {
        var parent = newBone.parent;
        while (parent != null)
        {
            var sameParent = FindSameBone(parent.name);
            if (sameParent == parent)
            {
                return;
            }
            else if (sameParent != null)
            {
                newBone.SetParent(sameParent, false);
                return;
            }
            else
            {
                newBone = newBone.parent;
                parent = newBone.parent;
            }
        }
    }
    void BatchBones(SkinnedMeshRenderer oldSMR, SkinnedMeshRenderer newSMR, List<Transform> newBonesStore)
    {
        var batchedBones = new List<Transform>();
        var newBones = new List<Transform>();
        foreach (Transform newBone in newSMR.bones)
        {
            var found = false;
            if (oldSMR != null)
            {
                foreach (Transform oldBone in oldSMR.bones)
                {
                    if (oldBone != null && newBone != null)
                    {
                        if (oldBone.name != newBone.name)
                        {
                            continue;
                        }
                        batchedBones.Add(oldBone);
                        found = true;
                        break;
                    }
                }
            }
            if (newBonesStore != null && !found)
            {
                //Avoid bone conflict
                var sameBone = FindSameBone(newBone.name);
                if (sameBone == null)
                {
                    BatchBone(newBone);
                    batchedBones.Add(newBone);
                    newBones.Add(newBone);
                }
                else
                {
                    batchedBones.Add(sameBone);
                }
            }
        }
        newSMR.bones = batchedBones.ToArray();
        if (!newBones.Contains(newSMR.rootBone))
        {
            if (oldSMR == null)
            {
                newSMR.rootBone = FindSameBone(newSMR.rootBone.name);
            }
            else
            {
                newSMR.rootBone = oldSMR.rootBone;
            }
        }
        if (newBonesStore != null)
        {
            newBonesStore.AddRange(newBones);
        }
    }
    void ReplacePart(Transform oldPart, Transform newPart, List<Transform> newBonesStore, bool replaceMaterial)
    {
        newPart.SetParent(transform);
        newPart.gameObject.layer = gameObject.layer;
        SkinnedMeshRenderer oldPartSMR = null;
        if (oldPart != null)
        {
            oldPartSMR = oldPart.GetComponent<SkinnedMeshRenderer>();
            oldPart.gameObject.SetActive(false);
        }
        var newPartSMR = newPart.GetComponent<SkinnedMeshRenderer>();
        BatchBones(oldPartSMR, newPartSMR, newBonesStore);
        if (oldPart != null && replaceMaterial)
        {
            newPartSMR.material = oldPartSMR.material;
        }
    }
    void ClearNewHeadParts()
    {
        for (int i = 0; i < newHeadParts.Count; ++i)
        {
            Destroy(newHeadParts[i].gameObject);
        }
        newHeadParts.Clear();
        for (int i = 0; i < newHeadBones.Count; ++i)
        {
            newHeadBones[i].SetParent(null);
            Destroy(newHeadBones[i].gameObject);
        }
        newHeadBones.Clear();
    }
    public void ReplaceHead(GameObject newHeadPrefab)
    {
        if (newHeadParts == null)
        {
            Init();
        }
        ClearNewHeadParts();
        var newHead = Instantiate(newHeadPrefab, headPart.position, headPart.rotation);
        for (int i = 0; i < newHead.transform.childCount; ++i)
        {
            var child = newHead.transform.GetChild(i);
            if (child.name == eyePart.name)
            {
                var newEyePart = child;
                ReplacePart(eyePart, newEyePart, newHeadBones, false);
                newHeadParts.Add(newEyePart);
                --i;
            }
            else if (child.name == hairPart.name)
            {
                var newHairPart = child;
                ReplacePart(hairPart, newHairPart, newHeadBones, false);
                newHeadParts.Add(newHairPart);
                --i;
            }
            else if (child.name == headPart.name)
            {
                var newHeadPart = child;
                ReplacePart(headPart, newHeadPart, newHeadBones, false);
                newHeadParts.Add(newHeadPart);
                --i;
            }
            else if (child.name != "root" && child.name != bodyPart.name && child.name != clothesPart.name && child.name != footballPart.name)
            {
                var newPart = child;
                ReplacePart(null, newPart, newHeadBones, false);
                newHeadParts.Add(newPart);
                --i;
            }
        }
        Destroy(newHead);
        animator.Rebind();
    }
    public void RecoverHead()
    {
        if (newHeadParts == null)
        {
            Init();
        }
        ClearNewHeadParts();
        eyePart.gameObject.SetActive(true);
        hairPart.gameObject.SetActive(true);
        headPart.gameObject.SetActive(true);
        animator.Rebind();
    }
    void ClearNewBodyParts()
    {
        for (int i = 0; i < newBodyParts.Count; ++i)
        {
            Destroy(newBodyParts[i].gameObject);
        }
        newBodyParts.Clear();
        for (int i = 0; i < newBodyBones.Count; ++i)
        {
            newBodyBones[i].SetParent(null);
            Destroy(newBodyBones[i].gameObject);
        }
        newBodyBones.Clear();
    }
    public void ReplaceBody(GameObject newBodyPrefab)
    {
        if (newBodyParts == null)
        {
            Init();
        }
        ClearNewBodyParts();
        var newBody = Instantiate(newBodyPrefab, bodyPart.position, bodyPart.rotation);
        for (int i = 0; i < newBody.transform.childCount; ++i)
        {
            var child = newBody.transform.GetChild(i);
            if (child.name == bodyPart.name)
            {
                var newBodyPart = child;
                ReplacePart(bodyPart, newBodyPart, null, true);
                newBodyParts.Add(newBodyPart);
                --i;
            }
            else if (child.name == clothesPart.name)
            {
                var newClothesPart = child;
                ReplacePart(clothesPart, newClothesPart, null, true);
                newHeadParts.Add(newClothesPart);
                --i;
            }
        }
        Destroy(newBody);
        animator.Rebind();
    }
    public void RecoverBody()
    {
        if (newBodyParts == null)
        {
            Init();
        }
        ClearNewBodyParts();
        bodyPart.gameObject.SetActive(true);
        clothesPart.gameObject.SetActive(true);
        animator.Rebind();
    }
    public void ReplaceFullSuit(GameObject newFullSuitPrefab)
    {
        if (newBodyParts == null || newHeadParts == null)
        {
            Init();
        }
        ClearNewHeadParts();
        ClearNewBodyParts();
        var newFullSuit = Instantiate(newFullSuitPrefab, bodyPart.position, bodyPart.rotation);
        for (int i = 0; i < newFullSuit.transform.childCount; ++i)
        {
            var child = newFullSuit.transform.GetChild(i);
            if (child.name == bodyPart.name)
            {
                var newBodyPart = child;
                ReplacePart(bodyPart, newBodyPart, null, false);
                newBodyParts.Add(newBodyPart);
                --i;
            }
            else if (child.name == clothesPart.name)
            {
                var newClothesPart = child;
                ReplacePart(clothesPart, newClothesPart, null, false);
                newHeadParts.Add(newClothesPart);
                --i;
            }
            else if (child.name == eyePart.name)
            {
                var newEyePart = child;
                ReplacePart(eyePart, newEyePart, newHeadBones, false);
                newHeadParts.Add(newEyePart);
                --i;
            }
            else if (child.name == hairPart.name)
            {
                var newHairPart = child;
                ReplacePart(hairPart, newHairPart, newHeadBones, false);
                newHeadParts.Add(newHairPart);
                --i;
            }
            else if (child.name == headPart.name)
            {
                var newHeadPart = child;
                ReplacePart(headPart, newHeadPart, newHeadBones, false);
                newHeadParts.Add(newHeadPart);
                --i;
            }
            else if (child.name != "root" && child.name != footballPart.name)
            {
                var newPart = child;
                ReplacePart(null, newPart, newHeadBones, false);
                newHeadParts.Add(newPart);
                --i;
            }
        }
        Destroy(newFullSuit);
        animator.Rebind();
    }
    public void RecoverFullSuit()
    {
        if (newBodyParts == null || newHeadParts == null)
        {
            Init();
        }
        ClearNewHeadParts();
        ClearNewBodyParts();
        eyePart.gameObject.SetActive(true);
        hairPart.gameObject.SetActive(true);
        headPart.gameObject.SetActive(true);
        bodyPart.gameObject.SetActive(true);
        clothesPart.gameObject.SetActive(true);
        animator.Rebind();
    }
    public void ReplaceCloth(Material material)
    {
        if (newBodyParts == null)
        {
            Init();
        }
        var smr = clothesPart.GetComponent<SkinnedMeshRenderer>();
        originalMaterial = smr.material;
        smr.material = material;
    }
    public void RecoverCloth()
    {
        if (newBodyParts == null)
        {
            Init();
        }
        var smr = clothesPart.GetComponent<SkinnedMeshRenderer>();
        smr.material = originalMaterial;
    }
    public void AttachBodyPart(GameObject newBodyPartPrefab)
    {
        if (newBodyParts == null)
        {
            Init();
        }
        ClearNewBodyParts();
        var newBodyPart = Instantiate(newBodyPartPrefab, bodyPart.position, bodyPart.rotation);
        for (int i = 0; i < newBodyPart.transform.childCount; ++i)
        {
            var child = newBodyPart.transform.GetChild(i);
            if (child.name != "root")
            {
                var newPart = child;
                ReplacePart(null, newPart, newBodyBones, false);
                newBodyParts.Add(newPart);
                --i;
            }
        }
        Destroy(newBodyPart);
    }
    public void DetachBodyPart()
    {
        if (newBodyParts == null)
        {
            Init();
        }
        ClearNewBodyParts();
    }
}
