#ifndef XPBD_TYPES_INCLUDED
#define XPBD_TYPES_INCLUDED

#define THREADS 256
#define MAX_COLLISIONS 8


// === Core Simulation ===
struct Particle
{
	float3 positionP;
	float3 positionX;
	float3 positionPredicted;
	float3 velocity;
	float m;
	float w;
	float radius;
};

struct DistanceConstraint
{
	uint i;
	uint j;
	float rest;
	float compliance;
};


// === Cloth / Broadphase ===
struct ClothRange
{
	uint start;
	uint count;
};

struct Aabb
{
	float3 mn;
	float3 mx;
};


// === Collision ===
struct CollisionConstraint
{
	float3 target;
	float3 normal;
	float radius;
    int rbIndex;
};

struct ImpulseEvent   
{
    int rbIndex;
    float3 pointWS;
    float3 J;
};

struct SphereCollider
{
	float3 center;
	float radius;
    int rbIndex;
};

struct CapsuleCollider
{
	float3 p0;
	float3 p1;
	float r;
    int rbIndex;
};

struct BoxCollider
{
	float3 center;
	float3 axisRight;
	float3 axisUp;
	float3 axisForward;
	float3 halfExtents;
    int rbIndex;
};

struct Triangle
{
	float3 a;
	float3 b;
	float3 c;
};

struct MeshRange
{
	uint start;
	uint count;
    int rbIndex;
};


// === Attachments ===
struct AttachmentObject
{
	float4x4 world;
};

struct AttachmentConstraint
{
    uint particle;
    uint objectIndex;
    float3 localPoint;
};


#endif
