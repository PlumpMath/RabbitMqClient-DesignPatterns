using RpcReceiver.Entities;

namespace RpcReceiver
{
    public interface IHeartbeatRepository
    {
        bool Insert(Group group);
        bool Update(Group group);

        bool Insert(Application application);
        bool Update(Application application);

        bool SaveAll();
    }
}