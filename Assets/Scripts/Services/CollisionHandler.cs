using UnityEngine;

[RequireComponent (typeof(BoxCollider2D))]
public class CollisionHandler : MonoBehaviour {

	public LayerMask collisionMask;
	[Range(0, 90)]
	public float maxClimbAngle = 75;
	[Range(0, 90)]
	public float maxDescendAngle = 75;
	public float skinWidth = 0.02f;

	const int HORIZONTAL_RAY_COUNT = 4;
	const int VERTICAL_RAY_COUNT = 6;
	
	float horizontalRaySpacing;
	float verticalRaySpacing;

	BoxCollider2D boxCollider;
	CollisionInfo collisions;
	RaycastOrigins raycastOrigins;

	void Awake() {
		boxCollider = GetComponent<BoxCollider2D>();

		CalculateRaySpacing ();
	}

	public Vector2 AdjustVelocityForCollisions(Vector2 velocity) {

		UpdateRaycastOrigins ();
		collisions.Reset();

		if (VectorUtil.IsMovingInXDirection(velocity)) {
			HorizontalCollisions (ref velocity);
		}
		if (VectorUtil.IsMovingInYDirection(velocity)) {
			VerticalCollisions (ref velocity);
		}
		
		return velocity;
	}

	public bool HasCollisionBelow() {
		return collisions.below;
	}

	public bool HasCollisionAbove() {
		return collisions.above;
	}
	
	void HorizontalCollisions(ref Vector2 velocity) {
		float directionX = Mathf.Sign(velocity.x);
		float rayLength = Mathf.Abs(velocity.x) + skinWidth;
		Vector2 rayOrigin = (VectorUtil.IsPositiveXDirection(velocity)) ? raycastOrigins.bottomRight : raycastOrigins.bottomLeft;
		
		for (int i = 0; i < HORIZONTAL_RAY_COUNT; i ++) {

			RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.right * directionX, rayLength, collisionMask);
			
			Debug.DrawRay(rayOrigin, Vector2.right * directionX * rayLength,Color.red);
			
			if (hit) {
				float slopeAngle = Vector2.Angle(hit.normal, Vector2.up);

				// Handle climbing slopes, only check using bottom (first) ray.
				if (i == 0 && slopeAngle <= maxClimbAngle) {
					collisions.slopeAngle = slopeAngle;

					// Remove distance to start of slope from velocity, if just starting.
					float distanceToSlopeStart = 0;
					if (collisions.IsStartingNewSlope()) {
						distanceToSlopeStart = hit.distance-skinWidth;
						velocity.x -= distanceToSlopeStart * directionX;
					}

					AdjustVelocityForSlope(ref velocity);

					// Re-add distance to slope start (
					velocity.x += distanceToSlopeStart * directionX;
				}

				// Handle non-climbable slope collision
				if (!collisions.climbingSlope || slopeAngle > maxClimbAngle) {
					velocity.x = (hit.distance - skinWidth) * directionX;
					rayLength = hit.distance;
					
					if (collisions.climbingSlope) {
						velocity.y = Mathf.Tan(collisions.slopeAngle * Mathf.Deg2Rad) * Mathf.Abs(velocity.x);
					}

					collisions.right = VectorUtil.IsPositiveXDirection(velocity);
					collisions.left = VectorUtil.IsNegativeXDirection(velocity);
				}
			}

			rayOrigin += Vector2.up * horizontalRaySpacing;
		}
	}

	void AdjustVelocityForSlope(ref Vector2 velocity) {
		float moveDistance = Mathf.Abs(velocity.x);
		float climbVelocityY = Mathf.Sin(collisions.slopeAngle * Mathf.Deg2Rad) * moveDistance;

		// If velocity is already greater than the climbing velocity, then we are jumping, so don't climb.
		if (velocity.y <= climbVelocityY) {
			velocity.y = climbVelocityY;
			velocity.x = Mathf.Cos(collisions.slopeAngle * Mathf.Deg2Rad) * moveDistance * Mathf.Sign(velocity.x);
			collisions.below = true;
			collisions.climbingSlope = true;
		}
	}
	
	void VerticalCollisions(ref Vector2 velocity) {
		if (velocity.y < 0) {
			DescendSlope(ref velocity);
		}

		float directionY = Mathf.Sign(velocity.y);
		float rayLength = Mathf.Abs(velocity.y) + skinWidth;
		
		for (int i = 0; i < VERTICAL_RAY_COUNT; i ++) {
			Vector2 rayOrigin = (Mathf.Approximately(directionY, -1))?raycastOrigins.bottomLeft:raycastOrigins.topLeft;
			rayOrigin += Vector2.right * (verticalRaySpacing * i + velocity.x);
			RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.up * directionY, rayLength, collisionMask);
			
			Debug.DrawRay(rayOrigin, Vector2.up * directionY * rayLength,Color.red);
			
			if (hit) {
				velocity.y = (hit.distance - skinWidth) * directionY;
				rayLength = hit.distance;
				
				if (collisions.climbingSlope) {
					velocity.x = velocity.y / Mathf.Tan(collisions.slopeAngle * Mathf.Deg2Rad) * Mathf.Sign(velocity.x);
				}
				
				collisions.below = Mathf.Approximately(directionY, -1);
				collisions.above = Mathf.Approximately(directionY, 1);
			}
		}
		
		if (collisions.climbingSlope) {
			float directionX = Mathf.Sign(velocity.x);
			rayLength = Mathf.Abs(velocity.x) + skinWidth;
			Vector2 rayOrigin = ((Mathf.Approximately(directionX, -1))?raycastOrigins.bottomLeft:raycastOrigins.bottomRight) + Vector2.up * velocity.y;
			RaycastHit2D hit = Physics2D.Raycast(rayOrigin,Vector2.right * directionX,rayLength,collisionMask);
			
			if (hit) {
				float slopeAngle = Vector2.Angle(hit.normal,Vector2.up);
				if (!Mathf.Approximately(slopeAngle, collisions.slopeAngle)) {
					velocity.x = (hit.distance - skinWidth) * directionX;
					collisions.slopeAngle = slopeAngle;
				}
			}
		}
	}
	
	void DescendSlope(ref Vector2 velocity) {
		float directionX = Mathf.Sign(velocity.x);
		Vector2 rayOrigin = (Mathf.Approximately(directionX, -1)) ? raycastOrigins.bottomRight : raycastOrigins.bottomLeft;
		RaycastHit2D hit = Physics2D.Raycast(rayOrigin, -Vector2.up, Mathf.Infinity, collisionMask);
		
		if (hit) {
			float slopeAngle = Vector2.Angle(hit.normal, Vector2.up);
			if (!Mathf.Approximately(slopeAngle, 0) && slopeAngle <= maxDescendAngle) {
				if (Mathf.Approximately(Mathf.Sign(hit.normal.x), directionX)) {
					if (hit.distance - skinWidth <= Mathf.Tan(slopeAngle * Mathf.Deg2Rad) * Mathf.Abs(velocity.x)) {
						float moveDistance = Mathf.Abs(velocity.x);
						float descendVelocityY = Mathf.Sin (slopeAngle * Mathf.Deg2Rad) * moveDistance;
						velocity.x = Mathf.Cos (slopeAngle * Mathf.Deg2Rad) * moveDistance * Mathf.Sign (velocity.x);
						velocity.y -= descendVelocityY;
						
						collisions.slopeAngle = slopeAngle;
						collisions.descendingSlope = true;
						collisions.below = true;
					}
				}
			}
		}
	}
	
	void UpdateRaycastOrigins() {
		Bounds bounds = boxCollider.bounds;
		bounds.Expand(skinWidth * -2);
		
		raycastOrigins.bottomLeft = new Vector2 (bounds.min.x, bounds.min.y);
		raycastOrigins.bottomRight = new Vector2 (bounds.max.x, bounds.min.y);
		raycastOrigins.topLeft = new Vector2 (bounds.min.x, bounds.max.y);
		raycastOrigins.topRight = new Vector2 (bounds.max.x, bounds.max.y);
	}
	
	void CalculateRaySpacing() {
		Bounds bounds = boxCollider.bounds;
		bounds.Expand(skinWidth * -2);
		
		horizontalRaySpacing = bounds.size.y / (HORIZONTAL_RAY_COUNT - 1);
		verticalRaySpacing = bounds.size.x / (VERTICAL_RAY_COUNT - 1);
	}
	
	struct CollisionInfo {
		public bool above, below;
		public bool left, right;
		
		public bool climbingSlope;
		public bool descendingSlope;
		public float slopeAngle, prevSlopeAngle;
		
		public void Reset() {
			above = below = false;
			left = right = false;
			climbingSlope = false;
			descendingSlope = false;
			
			prevSlopeAngle = slopeAngle;
			slopeAngle = 0;
		}

		public bool IsStartingNewSlope() {
			return !Mathf.Approximately(slopeAngle, prevSlopeAngle);
		}
	}

	struct RaycastOrigins {
		public Vector2 topLeft, topRight;
		public Vector2 bottomLeft, bottomRight;
	}
}