#!/usr/bin/env python3
# Prereqs: pip install mcp-sdk (or your org's MCP Python SDK package), set BLITZBRIDGE_URL and BLITZBRIDGE_TOKEN.
# Substitute: update target_profile value to one configured on your Blitz Bridge instance.

import asyncio
import os

from mcp.client.session import ClientSession
from mcp.client.streamable_http import streamablehttp_client


async def main() -> None:
    base_url = os.environ["BLITZBRIDGE_URL"].rstrip("/")
    token = os.environ["BLITZBRIDGE_TOKEN"]
    endpoint = f"{base_url}/mcp"
    headers = {"Authorization": f"Bearer {token}"}

    async with streamablehttp_client(endpoint, headers=headers) as (read_stream, write_stream, _):
        async with ClientSession(read_stream, write_stream) as session:
            await session.initialize()

            tools = await session.list_tools()
            print("Available tools:")
            for tool in tools.tools:
                print(f"- {tool.name}")

            result = await session.call_tool(
                "azure_sql_target_capabilities",
                {"target_profile": "primary-sql-target"},
            )
            print("\nTarget capabilities result:")
            print(result)


if __name__ == "__main__":
    asyncio.run(main())
