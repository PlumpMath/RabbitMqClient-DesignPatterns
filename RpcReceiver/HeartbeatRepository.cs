using System;
using RpcReceiver.Entities;

namespace RpcReceiver
{
    public class HeartbeatRepository:IHeartbeatRepository
    {
        private HeartbeatContext _ctx;

        public HeartbeatRepository(HeartbeatContext ctx)
        {
            _ctx = ctx;
        }

        public bool Insert(Group group)
        {
            try
            {
                _ctx.Group.Add(group);
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return false;
            }
        }

        public bool Update(Group group)
        {
            throw new System.NotImplementedException();
        }

        public bool Insert(Application application)
        {
            try
            {
                _ctx.Application.Add(application);
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return false;
            }
        }

        public bool Update(Application application)
        {
            throw new System.NotImplementedException();
        }

        public bool SaveAll()
        {
            return _ctx.SaveChanges() > 0;
        }
    }
}