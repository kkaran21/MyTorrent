using AutoMapper;
using MyTorrentBackend.Dtos;

namespace MyTorrentBackend.Profiles
{
    public class TorrentProfile : Profile
    {
        public TorrentProfile()
        {
            CreateMap<Dictionary<string, object>, TorrentFile>()
            .ForMember(dest => dest.Announce, opt => opt.MapFrom(src => src["announce"].ToString()))
            .ForMember(dest => dest.InfoHash, opt => opt.MapFrom(src => src["info hash"]))
            .ForMember(dest => dest.Length, opt => opt.MapFrom(src => ((Dictionary<string, object>)src["info"]).ContainsKey("length") ? ((Dictionary<string, object>)src["info"])["length"] : null))
            .ForMember(dest => dest.AnnounceList, opt => opt.MapFrom(src => src.ContainsKey("announce-list") ? src["announce-list"] : null))
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => ((Dictionary<string, object>)src["info"])["name"].ToString()))
            .ForMember(dest => dest.PieceLength, opt => opt.MapFrom(src => ((Dictionary<string, object>)src["info"])["piece length"]))
            .ForMember(dest => dest.Pieces, opt => opt.MapFrom(src => ((Dictionary<string, object>)src["info"]).ContainsKey("pieces") ? ((Dictionary<string, object>)src["info"])["pieces"] : null))
            .ForMember(dest => dest.Files, opt => opt.MapFrom(src => ((Dictionary<string, object>)src["info"]).ContainsKey("files") ? ((Dictionary<string, object>)src["info"])["files"] : null));
 
            CreateMap<Dictionary<string, object>, TrackerResponse>()
            .ForMember(dest => dest.Complete, opt => opt.MapFrom(src => src.ContainsKey("complete") ? src["complete"] : null))
            .ForMember(dest => dest.Incomplete, opt => opt.MapFrom(src => src.ContainsKey("incomplete") ? src["incomplete"] : null))
            .ForMember(dest => dest.FailureReason, opt => opt.MapFrom(src => src.ContainsKey("failure reason") ? src["failure reason"] : null))
            .ForMember(dest => dest.Interval, opt => opt.MapFrom(src => src.ContainsKey("interval") ? src["interval"] : null))
            .ForMember(dest => dest.TrackerId, opt => opt.MapFrom(src => src.ContainsKey("tracker id") ? src["tracker id"] : null))
            .ForMember(dest => dest.WarningMessage, opt => opt.MapFrom(src => src.ContainsKey("warning message") ? src["warning message"] : null))
            .ForMember(dest => dest.Peers, opt => opt.MapFrom(src => src.ContainsKey("peers") ? src["peers"] : null));
        }
    }
}