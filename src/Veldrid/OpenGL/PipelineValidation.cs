using static Veldrid.OpenGLBinding.OpenGLNative;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

#if DEBUG && (GL_VALIDATE_VERTEX_INPUT_ELEMENTS || GL_VALIDATE_SHADER_RESOURCE_NAMES)
namespace Veldrid.OpenGL
{
    static class PipelineValidation
    {
        static void ValidationFailed(string pipelineName, string message)
        {
            message = $"PIPELINE VALIDATION: {pipelineName} {message}";
            Debug.WriteLine(message);
            // throw new InvalidOperationException(message);
        }

#if DEBUG && GL_VALIDATE_VERTEX_INPUT_ELEMENTS
        public static unsafe void ValidateAttributes(string pipelineName, uint program, VertexLayoutDescription[] VertexLayouts)
        {
            uint attribNameByteCount = 64;
            byte* attribNamePtr = stackalloc byte[(int) attribNameByteCount];

            foreach (VertexLayoutDescription layoutDesc in VertexLayouts)
            {
                for (int i = 0; i < layoutDesc.Elements.Length; i++)
                {
                    string elementName = layoutDesc.Elements[i].Name;
                    int byteCount = Encoding.UTF8.GetByteCount(elementName) + 1;
                    byte* elementNamePtr = stackalloc byte[byteCount];
                    fixed (char* charPtr = elementName)
                    {
                        int bytesWritten = Encoding.UTF8.GetBytes(charPtr, elementName.Length, elementNamePtr, byteCount);
                        Debug.Assert(bytesWritten == byteCount - 1);
                    }

                    elementNamePtr[byteCount - 1] = 0; // Add null terminator.

                    int location = glGetAttribLocation(program, elementNamePtr);
                    if (location == -1)
                    {
                        uint attribIndex = 0;
                        var names = new List<string>();
                        while (true)
                        {
                            uint actualLength;
                            int size;
                            uint type;
                            glGetActiveAttrib(program, attribIndex, attribNameByteCount, &actualLength, &size, &type,
                                attribNamePtr);

                            if (glGetError() != 0)
                                break;

                            string name = Encoding.UTF8.GetString(attribNamePtr, (int) actualLength);
                            names.Add(name);
                            attribIndex++;
                        }

                        ValidationFailed(pipelineName,
                            $"There was no attribute variable with the name {elementName}." +
                            $" Valid names for this pipeline are: {string.Join(", ", names)}");
                    }
                }
            }
        }
#endif

#if DEBUG && GL_VALIDATE_SHADER_RESOURCE_NAMES
        public static unsafe void ReportInvalidBufferName(string pipelineName, uint program, string resourceName)
        {
            uint uniformBufferIndex = 0;
            uint bufferNameByteCount = 64;
            byte* bufferNamePtr = stackalloc byte[(int) bufferNameByteCount];
            var names = new List<string>();
            while (true)
            {
                uint actualLength;
                glGetActiveUniformBlockName(program, uniformBufferIndex, bufferNameByteCount, &actualLength, bufferNamePtr);

                if (glGetError() != 0)
                    break;

                string name = Encoding.UTF8.GetString(bufferNamePtr, (int) actualLength);
                names.Add(name);
                uniformBufferIndex++;
            }

            ValidationFailed(pipelineName,
                $"Unable to bind uniform buffer \"{resourceName}\" by name." +
                $" Valid names for this pipeline are: {string.Join(", ", names)}");
        }

        public static unsafe void ReportInvalidResourceName(string pipelineName, uint program, string resourceName)
        {
            uint uniformIndex = 0;
            uint resourceNameByteCount = 64;
            byte* resourceNamePtr = stackalloc byte[(int)resourceNameByteCount];

            var names = new List<string>();
            while (true)
            {
                uint actualLength;
                int size;
                uint type;
                glGetActiveUniform(program, uniformIndex, resourceNameByteCount,
                    &actualLength, &size, &type, resourceNamePtr);

                if (glGetError() != 0)
                    break;

                string name = Encoding.UTF8.GetString(resourceNamePtr, (int)actualLength);
                names.Add(name);
                uniformIndex++;
            }

            ValidationFailed(pipelineName,
                $"Unable to bind uniform \"{resourceName}\" by name." +
                $" Valid names for this pipeline are: {string.Join(", ", names)}");
        }
#endif

    }
}
#endif
