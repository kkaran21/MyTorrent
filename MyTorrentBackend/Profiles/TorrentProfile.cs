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
        }
    }
}