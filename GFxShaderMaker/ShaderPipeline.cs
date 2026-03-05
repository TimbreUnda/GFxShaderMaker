namespace GFxShaderMaker;

public class ShaderPipeline
{
	public enum PipelineType
	{
		Vertex,
		Fragment,
		Geometry,
		Hull,
		Domain,
		Compute
	}

	public PipelineType Type { get; private set; }

	public string Name => GetPipelineName(Type);

	public char Letter => GetPipelineLetter(Type);

	public static string GetPipelineName(PipelineType type)
	{
		return type switch
		{
			PipelineType.Fragment => "Frag", 
			PipelineType.Geometry => "Geometry", 
			PipelineType.Hull => "Hull", 
			PipelineType.Domain => "Domain", 
			PipelineType.Compute => "Compute", 
			_ => "Vertex", 
		};
	}

	public static char GetPipelineLetter(PipelineType type)
	{
		return GetPipelineName(type)[0];
	}

	public ShaderPipeline(PipelineType type)
	{
		Type = type;
	}
}
