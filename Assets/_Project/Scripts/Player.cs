using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class Player : NetworkBehaviour, IKitchenObjectParent
{
    public static Player LocalInstance { get; private set; }

    public static event EventHandler OnAnyPlayerSpawned;
    public static event EventHandler OnAnyPickedUpSomething;

    public event EventHandler<OnSelectedCounterChangedEventArgs> OnSelectedCounterChanged;
    public class OnSelectedCounterChangedEventArgs : EventArgs
    {
        public BaseCounter selectedCounter;
    }

    [SerializeField] private GameObject kitchenObjectHoldPoint;
    [SerializeField] private LayerMask countersLayerMask;
    [SerializeField] private float moveSpeed;
    [SerializeField] private float rotateSpeed;

    private BaseCounter selectedCounter;
    private KitchenObject kitchenObject;
    private Vector3 lastInteractDir;
    private bool isWalking;

    private void Start()
    {
        GameInput.instance.OnInteractAction += OnInteractAction;
        GameInput.instance.OnInteractAlternateAction += OnInteractAlternateAction;
    }

    private void Update()
    {
        if (!IsOwner)
            return;

        HandleMovement();
        HandleInteractions();
    }

    private void OnApplicationQuit()
    {
        GameInput.instance.OnInteractAction -= OnInteractAction;
        GameInput.instance.OnInteractAlternateAction -= OnInteractAlternateAction;
    }

    public static void ResetStaticData()
    {
        OnAnyPlayerSpawned = null;
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            LocalInstance = this;
        }

        OnAnyPlayerSpawned?.Invoke(this, EventArgs.Empty);
    }

    public GameObject GetKitchenObjectFollowGameObject()
    {
        return kitchenObjectHoldPoint;
    }
    public KitchenObject GetKitchenObject()
    {
        return kitchenObject;
    }

    public bool HasKitchenObject()
    {
        return kitchenObject != null;
    }

    public bool IsWalking()
    {
        return isWalking;
    }

    public void SetKitchenObject(KitchenObject kitchenObject)
    {
        this.kitchenObject = kitchenObject;

        if (kitchenObject != null)
        {
            OnAnyPickedUpSomething?.Invoke(this, EventArgs.Empty);
        }
    }

    public void ClearKitchenObject()
    {
        kitchenObject = null;
    }

    private void OnInteractAction(object sender, EventArgs e)
    {
        if (!GameManager.instance.IsGamePlaying())
            return;

        if (selectedCounter != null)
            selectedCounter.Interact(this);
    }

    private void OnInteractAlternateAction(object sender, EventArgs e)
    {
        if (!GameManager.instance.IsGamePlaying())
            return;

        if (selectedCounter != null)
            selectedCounter.InteractAlternate(this);
    }

    private void SetSelectedCounter(BaseCounter selectedCounter)
    {
        this.selectedCounter = selectedCounter;

        OnSelectedCounterChanged?.Invoke(this, new OnSelectedCounterChangedEventArgs
        {
            selectedCounter = selectedCounter
        });
    }

    private void HandleMovement()
    {
        Vector2 inputVector = GameInput.instance.GetMovementVectorNormalized();
        Vector3 moveDir = new(inputVector.x, 0f, inputVector.y);

        float moveDistance = moveSpeed * Time.deltaTime;
        float playerRadius = 0.7f;
        float playerHeight = 2;
        bool canMove = !Physics.CapsuleCast(transform.position, transform.position + Vector3.up * playerHeight, playerRadius, moveDir, moveDistance);

        if (!canMove)
        {
            // Attempt only X movement
            Vector3 moveDirX = new Vector3(moveDir.x, 0, 0).normalized;
            canMove = (moveDir.x < -0.5f || moveDir.x > 0.5f) && !Physics.CapsuleCast(transform.position, transform.position + Vector3.up * playerHeight, playerRadius, moveDirX, moveDistance);

            if (canMove)
            {
                // Can move only on the X
                moveDir = moveDirX;
            }
            else
            {
                // Cannot move only on the X
                // Attempt only Z movement
                Vector3 moveDirZ = new Vector3(0, 0, moveDir.z).normalized;
                canMove = (moveDir.z < -0.5f || moveDir.z > 0.5f) && !Physics.CapsuleCast(transform.position, transform.position + Vector3.up * playerHeight, playerRadius, moveDirZ, moveDistance);

                if (canMove)
                {
                    // Can move only on the Z
                    moveDir = moveDirZ;
                }
                else
                {
                    // Cannot move in any direction
                }
            }
        }

        if (canMove)
            transform.position += moveDistance * moveDir;

        isWalking = moveDir != Vector3.zero;

        transform.forward = Vector3.Slerp(transform.forward, moveDir, Time.deltaTime * rotateSpeed);
    }

    private void HandleInteractions()
    {
        Vector2 inputVector = GameInput.instance.GetMovementVectorNormalized();
        Vector3 moveDir = new(inputVector.x, 0f, inputVector.y);
        float interactDistance = 2f;

        if (moveDir != Vector3.zero)
            lastInteractDir = moveDir;

        if (Physics.Raycast(transform.position, lastInteractDir, out RaycastHit raycastHit, interactDistance, countersLayerMask))
        {
            if (raycastHit.transform.TryGetComponent(out BaseCounter baseCounter))
            {
                // Has ClearCounter
                if (baseCounter != selectedCounter)
                    SetSelectedCounter(baseCounter);
            }
            else
                SetSelectedCounter(null);
        }
        else
            SetSelectedCounter(null);
    }
}